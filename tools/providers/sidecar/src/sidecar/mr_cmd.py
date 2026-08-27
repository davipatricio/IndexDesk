"""``sidecar mr ...`` - MaisRetorno rentabilidade & catálogo via Next.js SSR.

Measured facts (recon 26/08/2026 - ``tools/providers/recon/recon-maisretorno.md``):

- The site is Next.js with every asset's payload embedded in ``__NEXT_DATA__``
  and also served at ``/_next/data/{buildId}/{tipo}/{slug}.json`` (no auth).
  ``buildId`` changes every deploy and is extracted from the HTML.
- Asset JSON shape (``pageProps``): ``cnpj``, ``nicename``, ``pageTitle``,
  ``stats.stats.timeframe`` (mtd/ytd/3-60M), and ``stats.years``: the full
  ``{ano: {"1".."12": %mes, "year": %ano, "accrued": %acum}}`` since the
  asset's first quote (PETR4/IBOV desde 1994).
- Stock list: ``/lista-acoes`` rawList 528 tickers (16 fields incl. ``cnpj``,
  ``code_cvm``, ``isin``, ``situation``); paginated ``/page/N`` (query param
  ``?page=`` ignored — path wins).
- Director lists: ``gestores`` 3.236 + ``administradores`` 334, paginated.
- Limitations: ZERO dividends/DY/PVP/PL/taxa adm on any page.
"""

from __future__ import annotations

import re
from typing import Any, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import SidecarError, fetch_error, parse_error, usage_error
from sidecar.schema import make_return_series

BASE_URL = "https://maisretorno.com"

REQUEST_TIMEOUT_SECONDS = 30.0


class ResponseLike(Protocol):
    """Minimal surface of a ``curl_cffi`` response used here."""

    status_code: int
    text: str

    def json(self) -> Any: ...


class HttpGet(Protocol):
    """Transport seam so offline tests can fake the HTTP layer."""

    def __call__(self, url: str, *, timeout: float) -> ResponseLike: ...


def _default_session_get() -> HttpGet:
    from curl_cffi import requests as curl_requests

    session = curl_requests.Session(impersonate="chrome")

    def get(url: str, *, timeout: float) -> ResponseLike:
        return session.get(url, timeout=timeout)

    return get


def _error_for_status(response: ResponseLike, path: str) -> None:
    if response.status_code == 403:
        raise SidecarError("Scrape.WafBlocked", f"MR blocked {path} (HTTP 403)", 3)
    if response.status_code != 200:
        raise fetch_error(f"MaisRetorno returned HTTP {response.status_code} for {path}")


def _resolve_build_id(get: HttpGet) -> tuple[str, HttpGet]:
    response = get(f"{BASE_URL}/", timeout=REQUEST_TIMEOUT_SECONDS)
    _error_for_status(response, BASE_URL)
    html = getattr(response, "text", "") or ""
    match = re.search(r'"buildId"\s*:\s*"([A-Za-z0-9_-]{6,40})"', html)
    if not match:
        raise parse_error("MaisRetorno: buildId not found on home page")
    return match.group(1), get


def _fetch_next_data_json(
    get: HttpGet,
    build_id: str,
    tipo: str,
    slug: str,
    page: int | None = None,
) -> dict[str, Any]:
    path = f"{tipo}/{slug}" if page in (None, 1) else f"{tipo}/{slug}/page/{page}"
    url = f"{BASE_URL}/_next/data/{build_id}/{path}.json"
    response = get(url, timeout=REQUEST_TIMEOUT_SECONDS)
    _error_for_status(response, url)
    try:
        payload = response.json()
    except Exception as exc:
        raise parse_error(f"MaisRetorno {tipo}/{slug}: invalid JSON ({exc})") from exc
    if not isinstance(payload, dict):
        raise parse_error(f"MaisRetorno {tipo}/{slug}: expected a JSON object")
    return payload


def returns(
    symbol: str,
    kind: str,
    fixture: str | None,
    *,
    get: HttpGet | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar mr returns`` (kind: return_series)."""
    if kind not in ("etf", "acoes", "fii", "indice"):
        raise usage_error(
            "--kind must be one of: etf, acoes, fii, indice "
            "(maps to the Next.js route segment)"
        )
    ticker = symbols.bare_symbol(symbol)

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "return_series")
        return

    slug = ticker.lower()

    with ndjson.stdout_guard():
        if get is None:
            get = _default_session_get()
        build_id, get = _resolve_build_id(get)
        payload = _fetch_next_data_json(get, build_id, kind, slug)
        page_props = payload.get("pageProps") if isinstance(payload.get("pageProps"), dict) else {}
        years = (page_props or {}).get("stats", {}).get("years") if isinstance(page_props, dict) else None
        # Top-level stats may live under pageProps.stats.years; fallback to pageProps.years
        if years is None:
            years = (page_props or {}).get("years")
        if not isinstance(years, dict):
            raise parse_error(f"MaisRetorno {kind}/{slug}: stats.years not found")
        records = []
        for year_key, year_payload in sorted(years.items()):
            if not isinstance(year_payload, dict):
                continue
            year_value = year_payload.get("year")
            for month_key, value in year_payload.items():
                if month_key in ("year", "accrued"):
                    continue
                if month_key not in ("1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"):
                    continue
                if value is None or value == "":
                    continue
                try:
                    ret = float(value)
                except (TypeError, ValueError):
                    continue
                month = int(month_key)
                records.append(
                    make_return_series(
                        ticker=ticker,
                        type_="monthly",
                        period=f"{int(year_key)}-{month:02d}",
                        ret=ret,
                    )
                )
            if isinstance(year_value, (int, float)):
                records.append(
                    make_return_series(
                        ticker=ticker,
                        type_="annual",
                        period=f"{int(year_key)}",
                        ret=float(year_value),
                    )
                )
        records.sort(key=lambda record: record["period"])

    for record in records:
        yield record, None
