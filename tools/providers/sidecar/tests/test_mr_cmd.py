"""``sidecar mr ...`` - MaisRetorno, fully offline (fake GET transports)."""

from __future__ import annotations

import json
from typing import Any

import pytest
from helpers import write_fixture
from sidecar import mr_cmd
from sidecar.errors import SidecarError


class FakeResponse:
    def __init__(
        self,
        payload: Any = None,
        *,
        status_code: int = 200,
        text: str = "",
    ) -> None:
        self._payload = payload
        self.status_code = status_code
        self.text = text

    def json(self) -> Any:
        if isinstance(self._payload, Exception):
            raise self._payload
        return self._payload


# ----- fixture: a minimal __NEXT_DATA__-style envelope -----

BUILD_ID = "gUhXo-UNCu8LNMvapdPrm"


def home_page() -> FakeResponse:
    return FakeResponse(text='<script>{"buildId":"' + BUILD_ID + '"}</script>')


def asset_page(years: dict[str, Any] | None = None) -> FakeResponse:
    payload = {
        "pageProps": {
            "stats": {"years": years or {"2023": {"1": 1.1, "2": 2.2, "year": 3.3}, "2024": {"1": 0.5, "year": 6.6}}},
            "cnpj": "123",
            "nicename": "AAA11",
        }
    }
    return FakeResponse(payload)


class Recorder:
    def __init__(self, responses: list[FakeResponse]) -> None:
        self.responses = responses
        self.urls: list[str] = []

    def __call__(self, url: str, *, timeout: float) -> FakeResponse:
        self.urls.append(url)
        return self.responses.pop(0)


def test_returns_maps_monthly_and_annual():
    transport = Recorder([home_page(), asset_page()])

    records = [r for r, _ in mr_cmd.returns("WRLD11", "etf", None, get=transport)]

    by_period = {r["period"]: r for r in records}
    assert by_period["2023-01"] == {"ticker": "WRLD11", "type": "monthly", "period": "2023-01", "ret": 1.1, "metric": "return"}
    assert by_period["2023-02"]["ret"] == 2.2
    assert by_period["2023"]["type"] == "annual"
    assert by_period["2023"]["ret"] == 3.3
    # Home GET to discover buildId, then asset JSON via _next/data.
    assert transport.urls[0] == mr_cmd.BASE_URL + "/"
    assert "/_next/data/" + BUILD_ID in transport.urls[1]


def test_returns_build_id_missing_is_parse_error():
    transport = Recorder([FakeResponse(text="<html>no buildId</html>")])

    with pytest.raises(SidecarError) as excinfo:
        list(mr_cmd.returns("PETR4", "acoes", None, get=transport))

    assert excinfo.value.code == "Parse.Invalid"


def test_returns_invalid_kind_is_usage_error():
    with pytest.raises(SidecarError) as excinfo:
        list(mr_cmd.returns("PETR4", "fundo", None, get=Recorder([])))
    assert excinfo.value.code == "Usage.Invalid"


def test_returns_bare_symbol_validation():
    with pytest.raises(SidecarError) as excinfo:
        list(mr_cmd.returns("^BVSP", "acoes", None, get=Recorder([])))
    assert excinfo.value.code == "Usage.Invalid"


def test_returns_fixture_roundtrip(tmp_path):
    fixture = write_fixture(
        tmp_path / "rs.ndjson",
        [{"ticker": "PETR4", "type": "monthly", "period": "2024-01", "ret": 1.23, "metric": "return"}],
    )
    records = [r for r, _ in mr_cmd.returns("PETR4", "acoes", fixture)]
    assert records[0]["period"] == "2024-01"


def test_returns_skips_null_entries():
    transport = Recorder(
        [
            home_page(),
            asset_page({"2023": {"1": None, "2": "", "3": 0.7, "year": 0.7}}),
        ]
    )
    records = [r for r, _ in mr_cmd.returns("KNCR11", "fii", None, get=transport)]
    types = [r["type"] for r in records]
    assert "monthly" in types and "annual" in types and len(types) == 2
    assert records[0]["ret"] == 0.7
