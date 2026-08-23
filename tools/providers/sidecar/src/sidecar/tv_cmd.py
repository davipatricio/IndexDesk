"""``sidecar tv history`` - TradingView OHLCV via tv-scraper 1.5.1.

Measured facts (Fase 0) that shape this module:

- The 1.5.1 API is ``CandleStreamer().get_candles(exchange=..., symbol=...,
  timeframe=..., numb_candles=N)`` returning
  ``{"status": "success"|"failed", "data": {"ohlcv": [{"index", "timestamp",
  "open", "high", "low", "close", "volume"?}, ...], ...}}``. It never raises:
  failures come back as ``status="failed"`` (or as exceptions from the socket
  layer, e.g. ``WebSocketTimeoutException``).
- Big payloads are flaky: BOVA11 with ``numb_candles >= 4500`` failed 3/3 while
  IBOV 5000 passed. Mitigation here: fetch in cumulative chunks of ~1000 bars
  walking backwards, retry each call (3 attempts, small backoff) and dedupe by
  timestamp.
- The library accepts at most 5000 candles per call and authenticates via a
  session cookie only (no email/password flow).
"""

from __future__ import annotations

import time
from datetime import UTC, datetime
from typing import Any, Iterable, Iterator, Protocol

from sidecar import ndjson, symbols
from sidecar.errors import fetch_error, usage_error
from sidecar.schema import make_quote

# tv-scraper TIMEFRAME_LITERAL (core/validation_data.py), 1.5.1.
VALID_INTERVALS = ("1m", "5m", "15m", "30m", "1h", "2h", "4h", "1d", "1w", "1M")

CHUNK_SIZE = 1000  # per-call growth step; keeps payloads small enough to be reliable
MAX_PER_CALL = 5000  # hard limit enforced by tv-scraper itself
MAX_ATTEMPTS = 3
BACKOFF_SECONDS = 1.0


class CandleSource(Protocol):
    """Minimal surface of ``tv_scraper.CandleStreamer`` used here."""

    def get_candles(
        self,
        exchange: str,
        symbol: str,
        timeframe: str = ...,
        numb_candles: int = ...,
        indicators: list[tuple[str, str]] | None = ...,
    ) -> dict[str, Any]: ...


def merge_batch(by_timestamp: dict[int, dict[str, Any]], batch: Iterable[dict[str, Any]]) -> bool:
    """Merge candle dicts keyed by timestamp. Returns True when new rows landed."""
    added = False
    for candle in batch or []:
        ts = int(candle["timestamp"])
        if ts not in by_timestamp:
            by_timestamp[ts] = candle
            added = True
    return added


def _call_with_retry(
    source: CandleSource,
    exchange: str,
    symbol: str,
    interval: str,
    count: int,
    *,
    max_attempts: int,
    backoff_seconds: float,
    sleep: Any,
    log: Any,
) -> dict[str, Any] | None:
    """One chunk request with retries. Returns the raw response or None when exhausted."""
    last_error = "unknown error"
    for attempt in range(1, max_attempts + 1):
        try:
            response = source.get_candles(
                exchange=exchange,
                symbol=symbol,
                timeframe=interval,
                numb_candles=count,
            )
            if response.get("status") == "success":
                return response
            last_error = str(response.get("error") or "status=failed without error message")
        except Exception as exc:  # socket-layer failures (timeouts, resets)
            last_error = f"{type(exc).__name__}: {exc}"
        log(
            f"tv {exchange}:{symbol} numb_candles={count} attempt {attempt}/{max_attempts}"
            f" failed ({last_error})"
        )
        if attempt < max_attempts:
            sleep(backoff_seconds * attempt)
    return None


def collect_history(
    source: CandleSource,
    exchange: str,
    symbol: str,
    interval: str,
    bars: int,
    *,
    chunk_size: int = CHUNK_SIZE,
    max_per_call: int = MAX_PER_CALL,
    max_attempts: int = MAX_ATTEMPTS,
    backoff_seconds: float = BACKOFF_SECONDS,
    sleep: Any = time.sleep,
    log: Any = ndjson.log,
) -> list[dict[str, Any]]:
    """Fetch up to *bars* daily candles, oldest first, deduped by timestamp.

    Strategy: ask for growing windows (chunk_size, 2*chunk_size, ...) capped at
    *max_per_call*, keeping only candles not seen yet - effectively walking
    backwards through history without an offset API. Stops when enough bars
    are collected, a fully-retried call yields nothing new, or the per-call cap
    is reached. Raises :class:`SidecarError` (exit 3) when zero candles arrive;
    partial results below *bars* are returned with a stderr warning.
    """
    if bars <= 0:
        raise usage_error("--bars must be a positive integer")

    by_timestamp: dict[int, dict[str, Any]] = {}
    # Cumulative-window walk: each step requests a larger window and dedupes,
    # so payloads stay small early and only grow as far as --bars demands
    # (never more than tv-scraper's own 5000-per-call ceiling).
    window = min(bars, chunk_size, max_per_call)
    while len(by_timestamp) < bars and window <= max_per_call:
        response = _call_with_retry(
            source,
            exchange,
            symbol,
            interval,
            window,
            max_attempts=max_attempts,
            backoff_seconds=backoff_seconds,
            sleep=sleep,
            log=log,
        )
        if response is None:
            break  # this window never succeeded; keep whatever earlier chunks got
        ohlcv = (response.get("data") or {}).get("ohlcv") or []
        progressed = merge_batch(by_timestamp, ohlcv)
        if not progressed:
            break  # provider has no older data; asking again would stall
        window = min(window + chunk_size, bars, max_per_call)

    if not by_timestamp:
        raise fetch_error(
            f"TradingView returned no candles for {exchange}:{symbol}"
            f" after retries ({interval})"
        )

    if len(by_timestamp) < bars:
        log(
            f"tv {exchange}:{symbol}: partial result {len(by_timestamp)}/{bars} bars"
            " (provider window/reliability limit)"
        )

    return [by_timestamp[ts] for ts in sorted(by_timestamp)]


def to_quote_records(candles: list[dict[str, Any]], exchange: str, symbol: str) -> list[dict[str, Any]]:
    """Convert raw TV candles to contract quote records (adj_close = close)."""
    ticker = f"{exchange}:{symbol}"
    records = []
    for candle in candles:
        day = datetime.fromtimestamp(int(candle["timestamp"]), tz=UTC).date().isoformat()
        close = float(candle["close"])
        volume = candle.get("volume")
        if volume is None:
            volume = 0
        elif float(volume).is_integer():
            volume = int(volume)
        else:
            volume = float(volume)
        records.append(
            make_quote(
                ticker=ticker,
                date=day,
                open_=float(candle["open"]),
                high=float(candle["high"]),
                low=float(candle["low"]),
                close=close,
                adj_close=close,  # TV sends raw prices; adjustment happens upstream
                volume=volume,
            )
        )
    return records


def history(
    symbol: str,
    interval: str,
    bars: int,
    fixture: str | None,
    cookie: str | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar tv history``."""
    if interval not in VALID_INTERVALS:
        raise usage_error(f"--interval must be one of {', '.join(VALID_INTERVALS)}")

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "quote")
        return

    exchange, symbol_part = symbols.tv_symbol(symbol)

    with ndjson.stdout_guard():
        from tv_scraper import CandleStreamer

        streamer = CandleStreamer(cookie=cookie)
        ndjson.log(f"tv history {exchange}:{symbol_part} interval={interval} bars={bars}")
        candles = collect_history(streamer, exchange, symbol_part, interval, bars)
        records = to_quote_records(candles, exchange, symbol_part)

    for record in records:
        yield record, None
