"""``sidecar b3 ...`` - B3 catalog commands, fully offline (fake HTTP layer)."""

from __future__ import annotations

import base64
import json
from typing import Any

import pytest
from helpers import write_fixture
from sidecar import b3_cmd
from sidecar.errors import SidecarError
from sidecar.schema import COMPANY_KEYS


class FakeResponse:
    def __init__(self, payload: Any, status_code: int = 200) -> None:
        self._payload = payload
        self.status_code = status_code

    def json(self) -> Any:
        if isinstance(self._payload, Exception):
            raise self._payload
        return self._payload


def catalog_page(rows: list[dict[str, Any]], total: int) -> dict[str, Any]:
    return {
        "page": {"pageNumber": 1, "pageSize": 120, "totalRecords": total, "totalPages": 1},
        "results": rows,
    }


def company_row(ticker: str = "PETR4", cnpj: str = "33000167000101") -> dict[str, Any]:
    return {
        "codeCVM": "12345",
        "issuingCompany": f"{ticker} S.A.",
        "tradingName": ticker,
        "cnpj": cnpj,
        "marketIndicator": "99",
        "dateListing": "31/12/9999",
    }


class Recorder:
    """Fake transport capturing URLs and replaying canned responses in order."""

    def __init__(self, responses: list[FakeResponse]) -> None:
        self.responses = responses
        self.urls: list[str] = []

    def __call__(self, url: str, *, timeout: float) -> FakeResponse:
        self.urls.append(url)
        return self.responses.pop(0)


def decode_last_filter(url: str) -> dict[str, Any]:
    token = url.rsplit("/", 1)[-1]
    return json.loads(base64.b64decode(token))


WARMUP = FakeResponse({"ok": True})  # app page warm-up (cookies)


def test_companies_walks_pages_and_maps_fields():
    transport = Recorder(
        [
            WARMUP,
            FakeResponse(catalog_page([company_row("AAA4"), company_row("BBB3")], 3)),
            FakeResponse(catalog_page([company_row("CCC3")], 3)),
        ]
    )

    records = [r for r, _ in b3_cmd.companies(None, get=transport)]

    assert [r["issuing_company"] for r in records] == [
        "AAA4 S.A.",
        "BBB3 S.A.",
        "CCC3 S.A.",
    ]
    first = records[0]
    assert set(first) == set(COMPANY_KEYS)  # contract has no ticker field
    assert first["cnpj"] == "33000167000101"
    assert first["code_cvm"] == "12345"

    # First call warms the app page; then one GET per page with the base64 filter.
    assert transport.urls[0] == b3_cmd.COMPANIES_APP_URL
    flt = decode_last_filter(transport.urls[-1])
    assert flt["codeCategoryBVMF"] == -1
    assert flt["language"] == "pt-br"
    assert flt["pageNumber"] == 2


def test_companies_stops_at_max_records():
    transport = Recorder(
        [
            WARMUP,
            FakeResponse(
                catalog_page([company_row(f"T{i}") for i in range(10)], 1000)
            ),
        ]
    )

    records = [r for r, _ in b3_cmd.companies(None, get=transport, max_records=3)]

    assert len(records) == 3


def test_companies_missing_issuing_company_is_parse_error():
    transport = Recorder([WARMUP, FakeResponse(catalog_page([{"cnpj": "1"}], 1))])

    with pytest.raises(SidecarError) as excinfo:
        list(b3_cmd.companies(None, get=transport))

    assert excinfo.value.code == "Parse.Invalid"
    assert excinfo.value.exit_code == 4


def test_companies_waf_block_maps_to_scrape_wafblocked():
    transport = Recorder([FakeResponse({"message": "Access Denied"}, status_code=403)])

    with pytest.raises(SidecarError) as excinfo:
        list(b3_cmd.companies(None, get=transport))

    assert excinfo.value.code == "Scrape.WafBlocked"
    assert excinfo.value.exit_code == 3


def test_companies_http_500_is_fetch_failure():
    transport = Recorder([FakeResponse({}, status_code=500)])

    with pytest.raises(SidecarError) as excinfo:
        list(b3_cmd.companies(None, get=transport))

    assert excinfo.value.code == "Fetch.Failed"


def test_companies_empty_body_is_invalid_json_not_empty_output():
    # Akamai gate without cookies answers 200 + empty body; must fail loudly.
    class EmptyBody(FakeResponse):
        def json(self) -> Any:
            raise ValueError("Expecting value")

    transport = Recorder([FakeResponse({"warm": True}), EmptyBody("")])

    with pytest.raises(SidecarError) as excinfo:
        list(b3_cmd.companies(None, get=transport))

    assert excinfo.value.code == "Parse.Invalid"


def test_fiis_sends_typefund_and_uppercases_ticker():
    transport = Recorder(
        [
            WARMUP,
            FakeResponse(
                catalog_page(
                    [
                        {"acronym": "adsh", "fundName": "AD SHOPPING FII"},
                        {"acronym": "HGLG", "fundName": "CSHG LOGÍSTICA"},
                    ],
                    2,
                )
            ),
        ]
    )

    records = [r for r, _ in b3_cmd.fiis(None, get=transport)]

    assert records == [
        {"ticker": "ADSH", "name": "AD SHOPPING FII", "fund_type": "FII"},
        {"ticker": "HGLG", "name": "CSHG LOGÍSTICA", "fund_type": "FII"},
    ]
    flt = decode_last_filter(transport.urls[-1])
    assert flt["typeFund"] == "FII"
    assert flt["keyword"] == ""
    assert flt["language"] == "pt-br"


def test_fiis_custom_type_reaches_the_filter():
    transport = Recorder([WARMUP, FakeResponse(catalog_page([], 0))])

    records = [r for r, _ in b3_cmd.fiis(None, fund_type="fidc", get=transport)]

    assert records == []  # empty result is valid output, not an error
    flt = decode_last_filter(transport.urls[-1])
    assert flt["typeFund"] == "FIDC"


def test_invalid_page_size_is_usage_error():
    with pytest.raises(SidecarError) as excinfo:
        list(b3_cmd.companies(None, get=Recorder([]), page_size=0))

    assert excinfo.value.code == "Usage.Invalid"
    assert excinfo.value.exit_code == 2


def test_fixture_mode_still_works_without_transport(tmp_path):
    fixture = write_fixture(
        tmp_path / "b3.ndjson",
        [{"cnpj": "1", "code_cvm": "2", "issuing_company": "X", "trading_name": "X",
          "market_indicator": "99", "date_listing": "01/02/2003"}],
    )

    records = [r for r, _ in b3_cmd.companies(fixture, get=Recorder([]))]

    assert [r["issuing_company"] for r in records] == ["X"]
