"""``sidecar im ...`` - InfoMoney commands, fully offline (fake HTTP layer)."""

from __future__ import annotations

import json
from typing import Any

import pytest
from helpers import dividend, quote, write_fixture
from sidecar import im_cmd
from sidecar.errors import SidecarError


class FakeResponse:
    def __init__(self, payload: Any, status_code: int = 200, text: str = "") -> None:
        self._payload = payload
        self.status_code = status_code
        self.text = text

    def json(self) -> Any:
        if isinstance(self._payload, Exception):
            raise self._payload
        return self._payload


def daily_page(rows: list[dict[str, Any]], has_next: bool) -> dict[str, Any]:
    return {"result": rows, "pageInfo": {"hasNextPage": has_next}}


def daily_row(day: str, close: float, volume: int = 1000) -> dict[str, Any]:
    return {
        "symbol": "MGLU3",
        "exchange": "B3",
        "tradeDate": f"{day}T03:00:00.000Z",
        "open": close - 0.5,
        "high": close + 0.5,
        "low": close - 1.0,
        "close": close,
        "change": 1.0,
        "tradeVolume": volume,
        "financialVolume": volume * close,
    }


class Recorder:
    """Fake transport capturing requests and replaying canned responses."""

    def __init__(self, responses: list[FakeResponse]) -> None:
        self.responses = responses
        self.calls: list[dict[str, Any]] = []

    def __call__(
        self,
        url: str,
        *,
        headers: dict[str, str],
        params: dict[str, Any],
        timeout: float,
    ) -> FakeResponse:
        self.calls.append({"url": url, "headers": headers, "params": params})
        return self.responses.pop(0)


KEY = "test-key-123"


def test_quotes_maps_fields_and_walks_pages():
    transport = Recorder(
        [
            FakeResponse(daily_page([daily_row("2026-08-21", 9.1), daily_row("2026-08-20", 9.0)], True)),
            FakeResponse(daily_page([daily_row("2026-08-19", 8.9)], False)),
        ]
    )

    records = [r for r, _ in im_cmd.quotes("MGLU3", bars=10, fixture=None, subscription_key=KEY, get=transport)]

    assert [r["date"] for r in records] == ["2026-08-19", "2026-08-20", "2026-08-21"]  # oldest first
    last = records[-1]
    assert last == {
        "ticker": "MGLU3",
        "date": "2026-08-21",
        "open": 8.6,
        "high": 9.6,
        "low": 8.1,
        "close": 9.1,
        "adj_close": 9.1,  # series is unadjusted
        "volume": 1000,
    }
    # Two pages walked with mandatory pagination params + auth header.
    assert len(transport.calls) == 2
    for index, call in enumerate(transport.calls, start=1):
        assert call["url"].endswith("b3/quotes/daily/MGLU3")
        assert call["params"]["Page"] == index
        assert call["params"]["Order"] == "Desc"
        assert call["headers"]["ocp-apim-subscription-key"] == KEY


def test_quotes_stops_when_target_bars_reached():
    transport = Recorder(
        [FakeResponse(daily_page([daily_row(f"2026-08-{d:02d}", 9.0) for d in range(11, 21)], True))]
    )

    records = [r for r, _ in im_cmd.quotes("MGLU3", bars=5, fixture=None, subscription_key=KEY, get=transport)]

    assert len(records) == 5
    assert len(transport.calls) == 1  # no extra page fetched once target met


def test_quotes_missing_subscription_key_is_discovered_from_page(monkeypatch):
    monkeypatch.delenv(im_cmd.SUBSCRIPTION_KEY_ENV, raising=False)
    page_html = (
        '<script>window.InfoMoneyPage={"api_marketdata":{'
        '"base_api_marketdata":"https://api","ocp_apim_subscription_key":"abc123def4567890"}};'
        "</script>"
    )
    transport = Recorder(
        [
            FakeResponse(None, text=page_html),  # warm-up/discovery GET
            FakeResponse(daily_page([daily_row("2026-08-21", 9.1)], False)),
        ]
    )

    records = [r for r, _ in im_cmd.quotes("MGLU3", bars=5, fixture=None, get=transport)]

    assert [r["date"] for r in records] == ["2026-08-21"]
    assert transport.calls[0]["url"] == im_cmd.DISCOVERY_URL
    assert transport.calls[1]["headers"]["ocp-apim-subscription-key"] == "abc123def4567890"


def test_quotes_env_key_skips_discovery_request(monkeypatch):
    monkeypatch.setenv(im_cmd.SUBSCRIPTION_KEY_ENV, "env-key-42")
    transport = Recorder(
        [FakeResponse(daily_page([daily_row("2026-08-21", 9.1)], False))]
    )

    records = [r for r, _ in im_cmd.quotes("MGLU3", bars=5, fixture=None, get=transport)]

    assert len(records) == 1
    assert len(transport.calls) == 1  # no discovery/warm-up GET on stateless fakes
    assert transport.calls[0]["headers"]["ocp-apim-subscription-key"] == "env-key-42"


def test_quotes_page_without_key_is_fetch_failure(monkeypatch):
    monkeypatch.delenv(im_cmd.SUBSCRIPTION_KEY_ENV, raising=False)
    transport = Recorder([FakeResponse(None, text="<html>no blob here</html>")])

    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("MGLU3", bars=5, fixture=None, get=transport))

    assert excinfo.value.code == "Fetch.Failed"


def test_quotes_discovery_waf_block_maps_to_scrape_wafblocked(monkeypatch):
    monkeypatch.delenv(im_cmd.SUBSCRIPTION_KEY_ENV, raising=False)
    transport = Recorder([FakeResponse(None, status_code=403)])

    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("MGLU3", bars=5, fixture=None, get=transport))

    assert excinfo.value.code == "Scrape.WafBlocked"


def test_quotes_waf_block_maps_to_scrape_wafblocked():
    transport = Recorder([FakeResponse({"message": "Access Denied"}, status_code=403)])

    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("MGLU3", bars=5, fixture=None, subscription_key=KEY, get=transport))

    assert excinfo.value.code == "Scrape.WafBlocked"
    assert excinfo.value.exit_code == 3


def test_quotes_bad_key_maps_to_scrape_authfailed():
    transport = Recorder([FakeResponse({"message": "invalid key"}, status_code=401)])

    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("MGLU3", bars=5, fixture=None, subscription_key=KEY, get=transport))

    assert excinfo.value.code == "Scrape.AuthFailed"


def test_quotes_http_500_is_generic_fetch_failure():
    transport = Recorder([FakeResponse({}, status_code=500)])

    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("MGLU3", bars=5, fixture=None, subscription_key=KEY, get=transport))

    assert excinfo.value.code == "Fetch.Failed"


def test_quotes_empty_result_is_fetch_failure_not_empty_output():
    transport = Recorder([FakeResponse(daily_page([], False))])

    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("MGLU3", bars=5, fixture=None, subscription_key=KEY, get=transport))

    assert excinfo.value.code == "Fetch.Failed"


def test_dividends_maps_b3_vocabulary_passthrough():
    transport = Recorder(
        [
            FakeResponse(
                {
                    "result": [
                        {
                            "type": "DIVIDENDO",
                            "rate": 0.15,
                            "lastDatePriorToEx": "2026-05-06T03:00:00.000Z",
                        },
                        {
                            "type": "JRSCAPPROPRIO",
                            "rate": 0.42,
                            "lastDatePriorToEx": "2026-02-11T03:00:00.000Z",
                        },
                    ],
                    "pageInfo": {"hasNextPage": False},
                }
            )
        ]
    )

    records = [r for r, _ in im_cmd.dividends("MGLU3", fixture=None, subscription_key=KEY, get=transport)]

    assert records == [
        dividend(ticker="MGLU3", date="2026-02-11", rate=0.42, type_="JRSCAPPROPRIO"),
        dividend(ticker="MGLU3", date="2026-05-06", rate=0.15, type_="DIVIDENDO"),
    ]


def test_dividends_empty_series_emits_zero_lines():
    transport = Recorder([FakeResponse(daily_page([], False))])

    records = [r for r, _ in im_cmd.dividends("BOVA11", fixture=None, subscription_key=KEY, get=transport)]

    assert records == []  # valid NDJSON output, not an error


def test_fixture_mode_still_works_without_transport(tmp_path):
    fixture = write_fixture(tmp_path / "im.ndjson", [quote(ticker="MGLU3")])

    records = [
        r
        for r, _ in im_cmd.quotes(
            "MGLU3", bars=5, fixture=fixture, subscription_key=KEY, get=Recorder([])
        )
    ]

    assert [r["ticker"] for r in records] == ["MGLU3"]
    assert json.dumps(records[0])  # stays JSON-serializable for ndjson.emit


def test_non_b3_symbol_rejected_as_usage_error():
    with pytest.raises(SidecarError) as excinfo:
        list(im_cmd.quotes("^BVSP", bars=5, fixture=None, subscription_key=KEY, get=Recorder([])))

    assert excinfo.value.code == "Usage.Invalid"
    assert excinfo.value.exit_code == 2
