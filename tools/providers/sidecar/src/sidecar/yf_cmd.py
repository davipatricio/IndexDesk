"""``sidecar yf ...`` - Yahoo Finance via yfinance (pinned 1.6.0).

Notes from Fase 0 + 26-27/08/2026 probes:
- B3 tickers need the ``.SA`` suffix (handled by :func:`sidecar.symbols.yf_symbol`).
- Dividend stream (events=div|split|earn) can be empty for many BDR-ETF/ETF
  B3 (VWRA11 0, BOVA11 0). So is Yahoo v8: events keys [] even with
  source=cosaic. MSN-style gap → not a client bug, data gap at Yahoo.
- Yahoo v8 `query1.finance.yahoo.com` needs triplo: cookies A1/A3/A1S,
  crumb from fc.yahoo.com, Origin https://finance.yahoo.com + Referer
  /chart/{T}.SA — curl_cffi impersonate=chrome covers it. User cookie
  optional via Providers__Yahoo__Cookie (warm-up fallback via sidecar if needed).
- Raw capture 27/08: source=cosaic, interval=1d, includePrePost, lang en-US,
  ATS + _SUPERFLY_lockout_finance cookie.
"""

from __future__ import annotations

import math
from typing import Any, Iterator

from sidecar import ndjson, symbols
from sidecar.schema import make_dividend, make_quote


def _iso_date(value: Any) -> str:
    """Coerce a pandas/stdlib date-like value to ``YYYY-MM-DD``."""
    if isinstance(value, str):
        return value[:10]
    return value.isoformat()[:10]


def quotes(
    symbol: str,
    start: str | None,
    end: str | None,
    fixture: str | None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Yield ``(record, raw_line)`` quote pairs; ``raw_line`` set for fixtures."""
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "quote")
        return

    # Deferred heavy import keeps --help and fixture mode cheap; the guard keeps
    # any stray library output away from the NDJSON stdout stream.
    with ndjson.stdout_guard():
        import yfinance as yf

        ticker = symbols.yf_symbol(symbol)
        ndjson.log(f"yf quotes {ticker} start={start} end={end}")
        df = yf.Ticker(ticker).history(
            start=start,
            end=end,
            interval="1d",
            auto_adjust=False,  # keep an explicit Adj Close column
        )

        adj_col = "Adj Close" if "Adj Close" in df.columns else None
        records: list[dict[str, Any]] = []
        for index, row in df.iterrows():
            close = row.get("Close")
            if close is None or math.isnan(float(close)):
                continue  # placeholder rows with no price carry no information
            adj = row.get(adj_col) if adj_col else None
            if adj is None or math.isnan(float(adj)):
                adj = close
            volume = row.get("Volume", 0)
            volume = 0 if volume is None or math.isnan(float(volume)) else int(volume)
            records.append(
                make_quote(
                    ticker=ticker,
                    date=_iso_date(index),
                    open_=float(row["Open"]),
                    high=float(row["High"]),
                    low=float(row["Low"]),
                    close=float(close),
                    adj_close=float(adj),
                    volume=volume,
                )
            )

    for record in records:
        yield record, None


def dividends(
    symbol: str,
    fixture: str | None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Yield ``(record, raw_line)`` dividend pairs; empty series emits nothing."""
    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "dividend")
        return

    with ndjson.stdout_guard():
        import yfinance as yf

        ticker = symbols.yf_symbol(symbol)
        ndjson.log(f"yf dividends {ticker}")
        series = yf.Ticker(ticker).dividends

        records = []
        for index, amount in series.items():  # empty Series => zero lines, exit 0
            rate = float(amount)
            if math.isnan(rate):
                continue
            records.append(
                make_dividend(
                    ticker=ticker,
                    date=_iso_date(getattr(index, "date", lambda: index)()),
                    rate=rate,
                    type_="DIVIDEND",
                )
            )

    for record in records:
        yield record, None
