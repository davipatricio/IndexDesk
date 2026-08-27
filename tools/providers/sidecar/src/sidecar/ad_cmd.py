"""``sidecar adv ...`` - ADVFN BR proventos (HTML server-rendered, grátis)."""

from __future__ import annotations

import re
from typing import Any, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import fetch_error, parse_error
from sidecar.schema import make_dividend

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
        raise fetch_error(f"ADVFN returned HTTP {response.status_code} for {url}")
    return getattr(response, "text", "") or ""


def dividends(
    symbol: str,
    fixture: str | None,
    *,
    get: HttpGet | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar adv dividends`` (HTML table)."""
    ticker = symbols.bare_symbol(symbol)
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "dividend")
        return

    if get is None:
        get = _default_session_get()

    # Dividends page: id="dividends-history-table" with data-ts unix seconds.
    url = f"https://br.advfn.com/bolsa-de-valores/bovespa/{ticker}/balanco/dividendos"
    html = _get_html(get, url)
    import time as _time  # noqa: F401  # keeps this module self-contained

    records: list[dict[str, Any]] = []
    seen: set[tuple[str, float, str]] = set()
    # <tr><td data-ts="...">24 Ago 2026</td><td title="Dividendos|Juros...">R$0,20</td>
    row_re = re.compile(
        r'<tr>\s*'
        r'<td[^>]*data-ts="(?P<ts>\d+)"[^>]*>(?P<label>[^<]+)</td>\s*'
        r'<td[^>]*title="(?P<title>[^"]*)"[^>]*>R\$(?P<val>[0-9]+[.,][0-9]+)',
        re.IGNORECASE,
    )
    for match in row_re.finditer(html):
        ts = int(match.group("ts"))
        title = match.group("title").strip()
        raw_val = match.group("val").strip()
        raw_val = raw_val.replace(".", "").replace(",", ".") if raw_val.count(",") == 1 else raw_val  # drop pt-BR milhares if present
        raw_val = raw_val.replace(".", "").replace(",", ".") if raw_val.count(".") > 1 else raw_val
        # pt-BR value like 0,2025 -> 0.2025 (only one comma is decimal)
        if raw_val.count(",") == 1:
            raw_val = raw_val.replace(",", ".")
        try:
            rate = float(raw_val.replace(",", ".").replace(".", ""))  # placeholder
            # cleaner: normalize properly (comma = decimal)
            norm = match.group("val").strip().replace(".", "").replace(",", ".")
            rate = float(norm)
        except ValueError:
            continue
        iso = _time.strftime("%Y-%m-%d", _time.gmtime(ts))
        label = "DIVIDENDO"
        if title and re.search(r"juros|jcp", title, re.IGNORECASE):
            label = "JCP"
        dedupe = (iso, round(rate, 10), label)
        if dedupe in seen or rate <= 0:
            continue
        seen.add(dedupe)
        records.append(make_dividend(ticker=ticker, date=iso, rate=rate, type_=label))

    if not records:
        return

    records.sort(key=lambda record: (record["date"], record["type"]))
    for record in records:
        yield record, None
