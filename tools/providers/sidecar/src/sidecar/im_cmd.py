"""``sidecar im ...`` - InfoMoney (XP Inc) market-data API via curl_cffi.

Measured facts (Fase 0.5 recon - ``tools/providers/recon/recon.md``):

- Host base ``https://api-infomoney.xpi.com.br/infomoney-services-marketdata/v1/api/v1/``.
- Auth = header ``ocp-apim-subscription-key`` (public frontend key). It is read
  from the ``INFOMONEY_SUBSCRIPTION_KEY`` environment variable, injected by the
  .NET runner from configuration - never logged and never an argv flag.
- Akamai WAF blocks non-browser TLS *before* validating the key: plain clients
  get 403 HTML, so requests go through ``curl_cffi`` with
  ``impersonate="chrome"``.
- ``b3/quotes/daily/{ticker}`` is paginated (``Page``/``PageSize``/``Order``
  are mandatory) and returns raw (unadjusted) prices: ``tradeDate``, ``open``,
  ``high``, ``low``, ``close``, ``tradeVolume``. No adjusted close exists -
  ``adj_close = close`` in the NDJSON contract.
- Dividends live at ``b3/corporate-events/cash-dividends/{ticker}``: ``rate``
  is the value per share in BRL, ``lastDatePriorToEx`` is the ex/com date and
  ``type`` uses the native B3 vocabulary (DIVIDENDO, JRSCAPPROPRIO, ...) which
  passes through untouched (JSCP matters for IR withholding rules).
"""

from __future__ import annotations

import os
from typing import Any, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import SidecarError, fetch_error, parse_error, usage_error
from sidecar.schema import make_dividend, make_quote

BASE_URL = (
    "https://api-infomoney.xpi.com.br"
    "/infomoney-services-marketdata/v1/api/v1/"
)
SUBSCRIPTION_KEY_ENV = "INFOMONEY_SUBSCRIPTION_KEY"

DEFAULT_PAGE_SIZE = 500
MAX_QUOTE_PAGES = 40  # safety stop: 40 x 500 = 20k bars ceiling per invocation
MAX_DIVIDEND_PAGES = 20
REQUEST_TIMEOUT_SECONDS = 30.0

_HEADERS = {
    "Accept": "application/json",
    "Origin": "https://www.infomoney.com.br",
    "Referer": "https://www.infomoney.com.br/",
}


class ResponseLike(Protocol):
    """Minimal surface of a ``curl_cffi`` response used here."""

    status_code: int

    def json(self) -> Any: ...


class HttpGet(Protocol):
    """Transport seam so offline tests can fake the HTTP layer."""

    def __call__(
        self,
        url: str,
        *,
        headers: dict[str, str],
        params: dict[str, Any],
        timeout: float,
    ) -> ResponseLike: ...


def _default_get(
    url: str,
    *,
    headers: dict[str, str],
    params: dict[str, Any],
    timeout: float,
) -> ResponseLike:
    from curl_cffi import requests as curl_requests

    return curl_requests.get(
        url,
        headers=headers,
        params=params,
        timeout=timeout,
        impersonate="chrome",  # Akamai WAF: TLS fingerprint must look like Chrome
    )


def _subscription_key(explicit: str | None) -> str:
    key = explicit or os.environ.get(SUBSCRIPTION_KEY_ENV, "").strip()
    if not key:
        raise fetch_error(
            f"{SUBSCRIPTION_KEY_ENV} is not set (injected by the .NET runner "
            "from Providers__InfoMoney__SubscriptionKeys__0)"
        )
    return key


def _error_for_status(response: ResponseLike, path: str) -> None:
    if response.status_code == 403:
        # Akamai block page: TLS fingerprint rejected before APIM auth ran.
        raise SidecarError("Scrape.WafBlocked", f"Akamai blocked {path} (HTTP 403)", 3)
    if response.status_code == 401:
        raise SidecarError("Scrape.AuthFailed", f"InfoMoney rejected the subscription key for {path} (HTTP 401)", 3)
    if response.status_code != 200:
        raise fetch_error(f"InfoMoney returned HTTP {response.status_code} for {path}")


def _decode_json(response: ResponseLike, path: str) -> dict[str, Any]:
    try:
        payload = response.json()
    except Exception as exc:
        raise parse_error(f"InfoMoney {path}: invalid JSON ({exc})") from exc
    if not isinstance(payload, dict):
        raise parse_error(f"InfoMoney {path}: expected a JSON object")
    return payload


def _paged_results(
    get: HttpGet,
    path: str,
    key: str,
    *,
    page_size: int,
    max_pages: int,
) -> Iterator[dict[str, Any]]:
    """Walk ``pageInfo.hasNextPage`` yielding raw result objects."""
    for page in range(1, max_pages + 1):
        response = get(
            f"{BASE_URL}{path}",
            headers={**_HEADERS, "ocp-apim-subscription-key": key},
            params={"Page": page, "PageSize": page_size, "Order": "Desc"},
            timeout=REQUEST_TIMEOUT_SECONDS,
        )
        _error_for_status(response, path)
        payload = _decode_json(response, path)
        results = payload.get("result") or []
        if not isinstance(results, list):
            raise parse_error(f"InfoMoney {path}: 'result' must be a list")
        yield from results
        page_info = payload.get("pageInfo") or {}
        if not page_info.get("hasNextPage"):
            return


def quotes(
    symbol: str,
    bars: int,
    fixture: str | None,
    *,
    subscription_key: str | None = None,
    get: HttpGet = _default_get,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar im quotes``."""
    if bars <= 0:
        raise usage_error("--bars must be a positive integer")

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "quote")
        return

    ticker = symbols.im_symbol(symbol)
    key = _subscription_key(subscription_key)

    with ndjson.stdout_guard():
        ndjson.log(f"im quotes {ticker} bars={bars}")
        by_date: dict[str, dict[str, Any]] = {}
        for item in _paged_results(
            get,
            f"b3/quotes/daily/{ticker}",
            key,
            page_size=min(DEFAULT_PAGE_SIZE, max(bars, 1)),
            max_pages=MAX_QUOTE_PAGES,
        ):
            day = str(item.get("tradeDate", ""))[:10]
            close = float(item.get("close", 0.0))
            volume = item.get("tradeVolume", 0)
            by_date[day] = {
                "day": day,
                "open": float(item.get("open", 0.0)),
                "high": float(item.get("high", 0.0)),
                "low": float(item.get("low", 0.0)),
                "close": close,
                # The daily series is unadjusted; adj_close mirrors close.
                "adj_close": close,
                "volume": int(volume) if float(volume).is_integer() else float(volume),
            }
            if len(by_date) >= bars:
                break

        if not by_date:
            raise fetch_error(f"InfoMoney returned no daily quotes for {ticker}")

        records = [
            make_quote(
                ticker=ticker,
                date=row["day"],
                open_=row["open"],
                high=row["high"],
                low=row["low"],
                close=row["close"],
                adj_close=row["adj_close"],
                volume=row["volume"],
            )
            for row in (by_date[d] for d in sorted(by_date))
        ]

    for record in records:
        yield record, None


def dividends(
    symbol: str,
    fixture: str | None,
    *,
    subscription_key: str | None = None,
    get: HttpGet = _default_get,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar im dividends`` (empty series emits nothing)."""
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "dividend")
        return

    ticker = symbols.im_symbol(symbol)
    key = _subscription_key(subscription_key)

    with ndjson.stdout_guard():
        ndjson.log(f"im dividends {ticker}")
        events = list(
            _paged_results(
                get,
                f"b3/corporate-events/cash-dividends/{ticker}",
                key,
                page_size=DEFAULT_PAGE_SIZE,
                max_pages=MAX_DIVIDEND_PAGES,
            )
        )
        records = []
        seen: set[tuple[str, float, str]] = set()
        for item in events:
            day = str(item.get("lastDatePriorToEx", ""))[:10]
            rate = float(item.get("rate", 0.0))
            type_ = str(item.get("type") or "DIVIDENDO")
            dedupe = (day, rate, type_)
            if not day or rate <= 0 or dedupe in seen:
                continue
            seen.add(dedupe)
            records.append(make_dividend(ticker=ticker, date=day, rate=rate, type_=type_))

        records.sort(key=lambda record: record["date"])

    for record in records:
        yield record, None
