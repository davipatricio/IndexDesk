"""``sidecar fe ...`` - FundsExplorer FII data via WordPress admin-ajax.

Measured facts (recon 26/08/2026 - ``tools/providers/recon/recon-fundsexplorer.md``):

- The legacy REST API ``/api/v1/funds/{ticker}`` is dead (HTTP 500 both cases).
- The detail page ``https://www.fundsexplorer.com.br/funds/{TICKER}`` embeds one
  nonce per action in ``data-nonce`` attributes; ``POST /wp-admin/admin-ajax.php``
  with body ``action=<a>&fund=<TICKER>`` + header ``X-CSRF-TOKEN: <nonce>`` works
  WITHOUT cookies. Cloudflare is present but passive for Chrome TLS.
- ``funds-get-income`` → monthly dividends since ~2016-06: rows carry
  ``data_base`` (dd/mm/yyyy), ``valor``, ``yeld``, ``cotacao_fechamento`` —
  all strings (pt-BR decimals). We map to the shared ``dividend`` kind using
  ``valor`` per share and ``data_base`` as the ex/com date.
- ``funds-get-quotations`` → daily close for a fixed ~5-year window
  (~1.248 points). NOTE: ``quotations`` arrives DOUBLY SERIALIZED (a JSON string
  inside the JSON envelope) with ``Date``/``d_Cotacao`` keys. Close-only:
  ``open = high = low = close``, ``volume = 0``.
- ``funds-get-patrimonials`` → monthly equity-per-share since ~2016-01: mapped
  to the ``return_series`` contract as ``metric = "equity_per_share"``.
"""

from __future__ import annotations

import json
import re
from typing import Any, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import SidecarError, fetch_error, parse_error
from sidecar.schema import make_dividend, make_quote, make_return_series

BASE_URL = "https://www.fundsexplorer.com.br"
DETAIL_URL = BASE_URL + "/funds/{ticker}"
AJAX_URL = BASE_URL + "/wp-admin/admin-ajax.php"

REQUEST_TIMEOUT_SECONDS = 30.0


class ResponseLike(Protocol):
    """Minimal surface of a ``curl_cffi`` response used here."""

    status_code: int
    text: str

    def json(self) -> Any: ...


class HttpPost(Protocol):
    """Transport seam so offline tests can fake the HTTP layer."""

    def __call__(
        self,
        url: str,
        *,
        data: dict[str, str],
        headers: dict[str, str],
        timeout: float,
    ) -> ResponseLike: ...


class HttpGetText(Protocol):
    """GET returning the raw page body (for nonce discovery)."""

    def __call__(self, url: str, *, timeout: float) -> ResponseLike: ...


def _default_post() -> HttpPost:
    from curl_cffi import requests as curl_requests

    session = curl_requests.Session(impersonate="chrome")

    def post(
        url: str,
        *,
        data: dict[str, str],
        headers: dict[str, str],
        timeout: float,
    ) -> ResponseLike:
        return session.post(url, data=data, headers=headers, timeout=timeout)

    return post


def _default_get() -> HttpGetText:
    from curl_cffi import requests as curl_requests

    session = curl_requests.Session(impersonate="chrome")

    def get(url: str, *, timeout: float) -> ResponseLike:
        return session.get(url, timeout=timeout)

    return get


def _error_for_status(response: ResponseLike, path: str) -> None:
    if response.status_code == 403:
        raise SidecarError(
            "Scrape.WafBlocked", f"Cloudflare blocked {path} (HTTP 403)", 3
        )
    if response.status_code != 200:
        raise fetch_error(f"FundsExplorer returned HTTP {response.status_code} for {path}")


def _fetch_nonce(get: HttpGetText | None, ticker: str, action: str) -> tuple[str, HttpPost]:
    """GET the detail page once and pull the nonce for *action* from ``data-nonce``."""
    if get is None:
        get = _default_get()
    url = DETAIL_URL.format(ticker=ticker)
    response = get(url, timeout=REQUEST_TIMEOUT_SECONDS)
    _error_for_status(response, url)
    html = getattr(response, "text", "") or ""
    # data-action="<action>" ... data-nonce="<10 hex chars>" (attribute order varies)
    pattern = re.compile(
        r'data-(?:action|js_fund_action)\s*=\s*"' + re.escape(action) + r'"[^>]*'
        r'data-nonce\s*=\s*"([0-9a-fA-F]{6,16})"',
        re.IGNORECASE,
    )
    match = pattern.search(html) or re.compile(
        r'data-nonce\s*=\s*"([0-9a-fA-F]{6,16})"[^>]*data-(?:action|js_fund_action)\s*=\s*"'
        + re.escape(action)
        + '"',
        re.IGNORECASE,
    ).search(html)
    if not match:
        raise parse_error(f"no data-nonce found on {url} for action {action}")
    return match.group(1), _default_post()


def _ajax_call(
    post: HttpPost,
    ticker: str,
    action: str,
    nonce: str,
) -> dict[str, Any]:
    response = post(
        AJAX_URL,
        data={"action": action, "fund": ticker},
        headers={
            "X-CSRF-TOKEN": nonce,
            "X-Requested-With": "XMLHttpRequest",
            "Accept": "application/json",
            "Referer": DETAIL_URL.format(ticker=ticker),
        },
        timeout=REQUEST_TIMEOUT_SECONDS,
    )
    _error_for_status(response, AJAX_URL)
    try:
        payload = response.json()
    except Exception as exc:
        raise parse_error(f"FundsExplorer ajax {action}: invalid JSON ({exc})") from exc
    if not isinstance(payload, dict) or not payload.get("success"):
        raise parse_error(f"FundsExplorer ajax {action}: unexpected envelope")
    return payload


def _br_decimal(value: Any) -> float:
    """Parse decimals from the income/quota/patr payloads.

    FundsExplorer mixes formats: '1.2500' (dot) and '0,10' (pt-BR comma).
    When a comma is present, dots are thousands separators; otherwise the
    value is read as a regular float.
    """
    if isinstance(value, (int, float)):
        return float(value)
    raw = str(value or "").strip()
    cleaned = raw.replace(".", "").replace(",", ".") if "," in raw else raw
    try:
        return float(cleaned)
    except ValueError as exc:
        raise parse_error(f"FundsExplorer: invalid numeric value {value!r}") from exc


def _br_date(value: Any) -> str:
    """Normalize upstream date strings to ISO YYYY-MM-DD."""
    raw = str(value or "").strip()
    # Recency check: income uses '2026-07-31' already (v2); quotations use '26/08/21'
    # (dd/mm/yy and even '26/08/21 00:00'); patrimonials: '2016-01-01' (yyyy-mm-dd).
    if re.match(r"^\d{4}-\d{2}-\d{2}", raw):
        return raw[:10]
    # Strip possible time suffix.
    raw = raw.strip().split()[0]
    parts = raw.split("/")
    if len(parts) != 3:
        raise parse_error(f"FundsExplorer: invalid date {value!r}")
    day, month, year = (p.strip() for p in parts)
    if len(year) == 2:  # quotations use '26/08/21'
        year = f"20{year}"
    return f"{year}-{int(month):02d}-{int(day):02d}"


def _decode_quotations(data: Any) -> list[dict[str, Any]]:
    """The quotations payload may be a plain list or the {'quotations': '<json>'}
    double-serialized envelope (measured upstream quirk).

    Upstream 26/08: the envelope is a LIST wrapping one dict whose
    ``quotations`` field is itself a JSON string (e.g.
    ``[{"post_title":"KNCR11","quotations":"[{\\"price\\":...,"date":...}]"}]``).
    A single-dict success envelope without the outer list was also seen on
    very old builds, hence both shapes.
    """
    inner = data
    if isinstance(data, list) and len(data) == 1 and isinstance(data[0], dict) and "quotations" in data[0]:
        inner = data[0]
    raw = inner.get("quotations") if isinstance(inner, dict) else inner
    if isinstance(raw, str):
        try:
            decoded = json.loads(raw)
        except Exception as exc:
            raise parse_error(f"FundsExplorer quotations double-encoded JSON invalid: {exc}") from exc
    elif isinstance(raw, list):
        decoded = raw
    else:
        decoded = []
    if not isinstance(decoded, list):
        raise parse_error("FundsExplorer quotations must decode to a list")
    return [row for row in decoded if isinstance(row, dict)]


_INCOME_DATE_KEYS = ("data_base", "data_pagamento", "Data_base")
_QUOTATION_DATE_KEYS = ("Date", "date", "DATA", "data", "dat", "DateTime", "datetime")
_QUOTATION_PRICE_KEYS = ("d_Cotacao", "valor", "close", "preco", "cota", "cotacao", "price", "Cotacao")


def _first_number(row: dict[str, Any], keys: tuple[str, ...]) -> float:
    for key in keys:
        if row.get(key) not in (None, ""):
            return _br_decimal(row[key])
    raise parse_error(f"FundsExplorer: none of {keys} present in row")


def _first_date(row: dict[str, Any], keys: tuple[str, ...]) -> str:
    for key in keys:
        if row.get(key):
            return _br_date(row[key])
    raise parse_error(f"FundsExplorer: none of {keys} present in row")


def income(
    symbol: str,
    fixture: str | None,
    *,
    get: HttpGetText | None = None,
    post: HttpPost | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar fe income`` (kind: dividend)."""
    ticker = symbols.bare_symbol(symbol)

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "dividend")
        return

    with ndjson.stdout_guard():
        if post is None:
            nonce, post = _fetch_nonce(get, ticker, "funds-get-income")
            payload = _ajax_call(post, ticker, "funds-get-income", nonce)
        else:
            # Tests/stateless injection: use a canned nonce.
            payload = _ajax_call(post, ticker, "funds-get-income", "0123456789")
        data = payload.get("data") or []
        if not isinstance(data, list):
            raise parse_error("FundsExplorer income: 'data' must be a list")
        records = []
        seen: set[tuple[str, float]] = set()
        for row in data:
            if not isinstance(row, dict):
                continue
            rate = _first_number(row, ("valor", "Valor"))
            raw_day = row.get("data_base") or row.get("data_pagamento") or row.get("Data_base")
            day = _br_date(raw_day) if raw_day else _first_date(row, _INCOME_DATE_KEYS)
            dedupe = (day, round(rate, 10))
            if not day or rate <= 0 or dedupe in seen:
                continue
            seen.add(dedupe)
            records.append(make_dividend(ticker=ticker, date=day, rate=rate, type_="RENDIMENTO"))
        records.sort(key=lambda record: record["date"])

    for record in records:
        yield record, None


def quotations(
    symbol: str,
    fixture: str | None,
    *,
    get: HttpGetText | None = None,
    post: HttpPost | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar fe quotations`` (kind: quote, close-only)."""
    ticker = symbols.bare_symbol(symbol)

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "quote")
        return

    with ndjson.stdout_guard():
        if post is None:
            nonce, post = _fetch_nonce(get, ticker, "funds-get-quotations")
            payload = _ajax_call(post, ticker, "funds-get-quotations", nonce)
        else:
            payload = _ajax_call(post, ticker, "funds-get-quotations", "0123456789")
        rows = _decode_quotations(payload.get("data"))
        by_date: dict[str, float] = {}
        for row in rows:
            day = _first_date(row, _QUOTATION_DATE_KEYS)
            price = _first_number(row, _QUOTATION_PRICE_KEYS)
            if price <= 0:
                continue
            by_date[day] = price  # last write wins; upstream is chronological
        records = [
            make_quote(
                ticker=ticker,
                date=day,
                open_=price,
                high=price,
                low=price,
                close=price,
                adj_close=price,
                volume=0,
            )
            for day, price in sorted(by_date.items())
        ]

    for record in records:
        yield record, None


def patrimonials(
    symbol: str,
    fixture: str | None,
    *,
    get: HttpGetText | None = None,
    post: HttpPost | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar fe patrimonials`` (kind: return_series metric)."""
    ticker = symbols.bare_symbol(symbol)

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "return_series")
        return

    with ndjson.stdout_guard():
        if post is None:
            nonce, post = _fetch_nonce(get, ticker, "funds-get-patrimonials")
            payload = _ajax_call(post, ticker, "funds-get-patrimonials", nonce)
        else:
            payload = _ajax_call(post, ticker, "funds-get-patrimonials", "0123456789")
        data = payload.get("data") or []
        if isinstance(data, dict):
            maybe_list = data.get("valor") or data.get("patrimonio")
            data = [data] if maybe_list is not None else data
        records = []
        for row in data:
            if not isinstance(row, dict):
                continue
            value = _first_number(row, ("valor", "patrimonio", "Patrimonio", "equity_per_share"))
            raw_period = str(row.get("periodo") or row.get("data_base") or row.get("Data") or row.get("date") or "")
            parts = re.match(r"^\s*(\d{4})[-\/](\d{1,2})", raw_period.strip())
            if parts is not None:
                period = f"{int(parts.group(1))}-{int(parts.group(2)):02d}"
            elif "/" in raw_period:
                month, year = raw_period.strip().split("/")[:2]
                period = f"{int(year)}-{int(month):02d}"
            else:
                period = raw_period.strip()
            if not period or value <= 0:
                continue
            records.append(
                make_return_series(
                    ticker=ticker,
                    type_="monthly",
                    period=period,
                    ret=value,
                    metric="equity_per_share",
                )
            )
        records.sort(key=lambda record: record["period"])

    for record in records:
        yield record, None
