"""``sidecar cf ...`` - ClubeFII lista + detalhe bruto (HTML)."""

from __future__ import annotations

import re
from typing import Any, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import fetch_error, parse_error
from sidecar.schema import make_fund

REQUEST_TIMEOUT_SECONDS = 30.0


class ResponseLike(Protocol):
    status_code: int
    text: str

    def json(self) -> Any: ...


class HttpGet(Protocol):
    def __call__(self, url: str, *, timeout: float) -> ResponseLike: ...


def _default_session_get() -> HttpGet:
    from curl_cffi import requests as curl_requests

    session = curl_requests.Session(impersonate="chrome")

    def get(url: str, *, timeout: float) -> ResponseLike:
        return session.get(url, timeout=timeout)

    return get


def _get_html(get: HttpGet, url: str) -> str:
    response = get(url, timeout=REQUEST_TIMEOUT_SECONDS)
    if response.status_code != 200:
        raise fetch_error(f"ClubeFII returned HTTP {response.status_code} for {url}")
    return getattr(response, "text", "") or ""


def lista(
    fixture: str | None,
    *,
    get: HttpGet | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar cf lista`` (kind: fund, all FIIs)."""
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "fund")
        return

    if get is None:
        get = _default_session_get()

    url = "https://www.clubefii.com.br/fundo_imobiliario_lista"
    html = _get_html(get, url)
    # Rows look like: <tr ... onclick="DoNav('/fiis/MXRF11')"> ... <a href="/fiis/MXRF11">MXRF11</a> ...
    # Fallback: every distinct code in an /fiis/<CODE> link (first match is 003H11 in header, skip).
    raw_rows = re.findall(
        r"DoNav\('/fiis/([A-Z0-9]{4,11})'\)[^<]*>.*?<a[^>]*>([^<]+)</a>",
        html,
        re.IGNORECASE | re.DOTALL,
    )
    records: list[dict[str, Any]] = []
    seen: set[str] = set()
    for code, name in raw_rows:
        ticker = code.strip().upper()
        # Dedupe; the same ticker appears twice per row (cod + nome cols).
        if ticker in seen or not re.match(r"^[A-Z0-9]{4,11}$", ticker):
            continue
        seen.add(ticker)
        name_clean = name.strip()[:160]
        records.append(make_fund(ticker=ticker, name=name_clean or ticker, fund_type="FII"))

    if not records:
        raise parse_error("ClubeFII lista: no rows found (selector changed?)")
    records.sort(key=lambda r: r["ticker"])

    for record in records:
        yield record, None


def fundos(
    symbol: str,
    fixture: str | None,
    *,
    get: HttpGet | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar cf fundos`` (minimal: just the target FII as fund)."""
    ticker = symbols.bare_symbol(symbol)
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "fund")
        return

    if get is None:
        get = _default_session_get()

    url = f"https://www.clubefii.com.br/fiis/{ticker}"
    html = _get_html(get, url)
    # Detail page title carries the fund name; fall back to ticker.
    m = re.search(r"<title[^>]*>([^<]+)</title>", html, re.I)
    name = m.group(1).strip()[:160] if m else ticker
    yield make_fund(ticker=ticker, name=name, fund_type="FII"), None
