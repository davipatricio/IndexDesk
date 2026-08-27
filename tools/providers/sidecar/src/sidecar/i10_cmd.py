"""``sidecar i10 ...`` - Investidor10 (investidor10.com.br) APIs GET sem auth."""

from __future__ import annotations

import json
from typing import Any, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import fetch_error, parse_error, usage_error
from sidecar.schema import make_quote, make_return_series

BASE_URL = "https://investidor10.com.br"

REQUEST_TIMEOUT_SECONDS = 30.0


class ResponseLike(Protocol):
    status_code: int
    text: str

    def json(self) -> Any: ...


class HttpGet(Protocol):
    def __call__(self, url: str, *, timeout: float) -> ResponseLike: ...


def _default_get() -> HttpGet:
    from curl_cffi import requests as curl_requests

    session = curl_requests.Session(impersonate="chrome")

    def get(url: str, *, timeout: float) -> ResponseLike:
        return session.get(url, timeout=timeout)

    return get


def _get_json(get: HttpGet, url: str, timeout: float = REQUEST_TIMEOUT_SECONDS) -> Any:
    response = get(url, timeout=timeout)
    if response.status_code != 200:
        raise fetch_error(f"Investidor10 returned HTTP {response.status_code} for {url}")
    try:
        return response.json()
    except Exception as exc:
        raise parse_error(f"Investidor10 {url}: invalid JSON ({exc})") from exc


def _norm_date_first(value: str) -> str:
    """Normalize upstream created_at/last_update to ISO YYYY-MM-DD."""
    raw = (value or "").strip()
    # Strip time suffix if present.
    date_part = raw.split()[0]
    # DD/MM/YYYY
    if "/" in date_part:
        day, month, year = date_part.split("/")
        return f"{int(year)}-{int(month):02d}-{int(day):02d}"
    # Already YYYY-MM-DD? (some endpoints like indices)
    if date_part.count("-") == 2:
        return date_part[:10]
    raise parse_error(f"Investidor10 date {value!r}")


def batch(
    tickers: str,
    fixture: str | None,
    *,
    get: HttpGet | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar i10 batch`` (quote batch, close-only)."""
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "quote")
        return

    csv = ",".join(t.strip().upper() for t in tickers.split(",") if t.strip())
    if not csv:
        raise usage_error("--tickers must list at least one B3 ticker")
    url = f"{BASE_URL}/api/cotacoes/batch?tickers={csv}"
    if get is None:
        get = _default_get()

    with ndjson.stdout_guard():
        data = _get_json(get, url)
        if not isinstance(data, dict):
            raise parse_error("Investidor10 batch: expected a JSON object")
        records = []
        for key, payload in data.items():
            if not isinstance(payload, dict):
                continue
            price = float(payload.get("price") or 0)
            last = str(payload.get("last_update") or "")
            if price <= 0 or not last:
                continue
            day = _norm_date_first(last)
            records.append(
                make_quote(
                    ticker=symbols.bare_symbol(key),
                    date=day,
                    open_=price,
                    high=price,
                    low=price,
                    close=price,
                    adj_close=price,
                    volume=0,
                )
            )

    for record in records:
        yield record, None


def acao(
    symbol: str,
    fixture: str | None,
    *,
    get: HttpGet | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar i10 acao`` (close-only series)."""
    ticker = symbols.bare_symbol(symbol)
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "quote")
        return

    if get is None:
        get = _default_get()
    url = f"{BASE_URL}/api/cotacoes/acao/chart/{ticker.lower()}/"
    with ndjson.stdout_guard():
        data = _get_json(get, url)
        # Shape: {"real":[{"price":..., "created_at":"DD/MM/YYYY HH:MM"}, ...], "dolar":..., "euro":...}
        series = data.get("real") if isinstance(data, dict) else data
        if not isinstance(series, list):
            raise parse_error(f"Investidor10 acao/chart: expected a list at {url}")
        by_date: dict[str, float] = {}
        for row in series:
            if not isinstance(row, dict):
                continue
            price = float(row.get("price") or 0)
            created = str(row.get("created_at") or row.get("last_update") or "")
            if price <= 0 or not created:
                continue
            day = _norm_date_first(created)
            by_date[day] = price
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
