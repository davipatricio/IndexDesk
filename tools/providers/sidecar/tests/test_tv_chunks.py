"""TV chunked fetching: dedupe, retry, partial results - with a fake streamer."""

from __future__ import annotations

from typing import Any

import pytest

from sidecar.errors import SidecarError
from sidecar.tv_cmd import collect_history, merge_batch, to_quote_records


class FlakyStreamer:
    """Stands in for tv_scraper.CandleStreamer without network.

    Behaves like the cumulative-window walk the real API forces: a request for
    N candles returns the *last* N of the universe. Can be told to fail the
    first `fail_times` calls with an exception (WebSocketTimeoutException-like)
    or with a failed status envelope.
    """

    def __init__(
        self,
        universe: list[dict[str, Any]],
        *,
        fail_times: int = 0,
        mode: str = "exception",
    ) -> None:
        self.universe = universe
        self.fail_times = fail_times
        self.mode = mode
        self.calls: list[int] = []

    def get_candles(self, exchange, symbol, timeframe="1d", numb_candles=10, indicators=None):
        self.calls.append(numb_candles)
        if self.fail_times > 0:
            self.fail_times -= 1
            if self.mode == "exception":
                raise TimeoutError("WebSocketTimeoutException (simulated)")
            return {"status": "failed", "data": None, "error": "No OHLCV data received from stream."}
        window = self.universe[-numb_candles:]
        return {
            "status": "success",
            "data": {"ohlcv": [dict(c) for c in window], "indicators": {}},
            "warnings": [],
            "error": None,
        }


def make_universe(n: int) -> list[dict[str, Any]]:
    # oldest first; timestamps 1..n so dedupe/ordering assertions stay obvious
    return [
        {
            "index": i,
            "timestamp": 1_700_000_000 + i * 86_400,
            "open": 10.0 + i,
            "high": 11.0 + i,
            "low": 9.0 + i,
            "close": 10.5 + i,
            "volume": 100 + i,
        }
        for i in range(n)
    ]


def test_merge_batch_dedupes_by_timestamp():
    store: dict[int, dict[str, Any]] = {}
    batch = [{"timestamp": 5}, {"timestamp": 6}]
    assert merge_batch(store, batch) is True
    assert merge_batch(store, batch) is False  # same timestamps again: no progress


def test_collect_walks_backwards_in_chunks_and_dedupes(monkeypatch):
    streamer = FlakyStreamer(make_universe(2500))
    no_sleep = lambda *_: None  # noqa: E731

    candles = collect_history(
        streamer, "BMFBOVESPA", "BOVA11", "1d", 2500,
        chunk_size=1000, sleep=no_sleep,
    )

    assert len(candles) == 2500
    assert len({c["timestamp"] for c in candles}) == 2500  # deduped
    assert [c["timestamp"] for c in candles] == sorted(c["timestamp"] for c in candles)  # oldest first
    # growing cumulative windows capped by --bars: 1000 -> 2000 -> 2500 (never more than needed)
    assert streamer.calls == [1000, 2000, 2500]


def test_retry_recovers_from_transient_failure(monkeypatch):
    streamer = FlakyStreamer(make_universe(1500), fail_times=1)

    candles = collect_history(
        streamer, "BMFBOVESPA", "PETR4", "1d", 1500,
        chunk_size=1000, max_attempts=3, backoff_seconds=0.0, sleep=lambda *_: None,
    )

    assert len(candles) == 1500
    assert streamer.calls[:2] == [1000, 1000]  # first chunk retried once after the transient failure


def test_all_attempts_failing_with_no_data_raises_fetch_error():
    streamer = FlakyStreamer([], fail_times=99)

    with pytest.raises(SidecarError) as excinfo:
        collect_history(
            streamer, "BMFBOVESPA", "BOVA11", "1d", 500,
            chunk_size=500, max_attempts=2, backoff_seconds=0.0, sleep=lambda *_: None,
        )
    assert excinfo.value.exit_code == 3
    assert excinfo.value.code == "Fetch.Failed"


def test_failed_status_envelope_counts_as_failure():
    class AlwaysFailed:
        calls = 0

        def get_candles(self, **_):
            AlwaysFailed.calls += 1
            return {"status": "failed", "data": None, "error": "No OHLCV data received from stream."}

    with pytest.raises(SidecarError) as excinfo:
        collect_history(
            AlwaysFailed(), "BMFBOVESPA", "BOVA11", "1d", 100,
            chunk_size=100, max_attempts=3, backoff_seconds=0.0, sleep=lambda *_: None,
        )
    assert excinfo.value.code == "Fetch.Failed"
    assert AlwaysFailed.calls == 3


def test_small_request_never_over_fetches():
    streamer = FlakyStreamer(make_universe(3000))

    candles = collect_history(
        streamer, "BMFBOVESPA", "PETR4", "1d", 50,
        chunk_size=1000, sleep=lambda *_: None,
    )

    assert len(candles) == 50
    assert streamer.calls == [50]  # one call, exactly the requested size


def test_partial_result_when_provider_has_less_than_requested():
    streamer = FlakyStreamer(make_universe(1200))

    candles = collect_history(
        streamer, "BMFBOVESPA", "BOVA11", "1d", 5000,
        chunk_size=1000, sleep=lambda *_: None,
    )

    assert len(candles) == 1200  # everything available, no exception


def test_to_quote_records_maps_fields_and_defaults_volume():
    candles = [
        {"timestamp": 1_758_240_000, "open": 1.0, "high": 2.0, "low": 0.5, "close": 1.5},
    ]

    records = to_quote_records(candles, "BMFBOVESPA", "BOVA11")

    record = records[0]
    assert record["ticker"] == "BMFBOVESPA:BOVA11"
    assert record["adj_close"] == record["close"]  # TV has no adj close
    assert record["volume"] == 0  # absent volume becomes 0, not null
