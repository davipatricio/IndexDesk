"""``sidecar fe ...`` - FundsExplorer, fully offline (fake transports)."""

from __future__ import annotations

import json
from typing import Any

import pytest
from helpers import write_fixture
from sidecar import fe_cmd
from sidecar.errors import SidecarError


class FakePage:
    def __init__(self, text: str, status_code: int = 200) -> None:
        self.text = text
        self.status_code = status_code

    def json(self) -> Any:
        raise RuntimeError("GET page has no JSON")


class FakeAjax:
    def __init__(self, payload: Any, status_code: int = 200) -> None:
        self._payload = payload
        self.status_code = status_code
        self.text = ""  # must not conflict with payload dict keys

    def json(self) -> Any:
        if isinstance(self._payload, Exception):
            raise self._payload
        return self._payload


# ----- nonce in page HTML -----

def html_with_nonce(action: str, nonce: str) -> str:
    return (
        '<div id="dividends" data-action="'
        + action
        + '" data-nonce="'
        + nonce
        + '"></div>'
    )


# ----- income -----

class PostRecorder:
    def __init__(self, response: FakeAjax) -> None:
        self.response = response
        self.calls: list[dict[str, Any]] = []

    def __call__(
        self,
        url: str,
        *,
        data: dict[str, str],
        headers: dict[str, str],
        timeout: float,
    ) -> FakeAjax:
        self.calls.append({"url": url, "data": data, "headers": headers})
        return self.response


def test_income_maps_fields_and_dedupes():
    post = PostRecorder(
        FakeAjax(
            {
                "success": True,
                "data": [
                    {"data_base": "2026-08-02", "valor": "0,10"},
                    {"data_base": "2026-08-02", "valor": "0,10"},  # duplicate
                    {"data_base": "2026-07-15", "valor": "0,09"},
                ],
            }
        )
    )

    records = [r for r, _ in fe_cmd.income("KNCR11", None, post=post)]

    assert [r["rate"] for r in records] == [0.09, 0.10]
    assert records[0]["type"] == "RENDIMENTO"
    assert post.calls[0]["data"] == {"action": "funds-get-income", "fund": "KNCR11"}
    assert post.calls[0]["headers"]["X-CSRF-TOKEN"] == "0123456789"


def test_income_empty_series_emits_nothing():
    post = PostRecorder(FakeAjax({"success": True, "data": []}))
    assert list(fe_cmd.income("KNCR11", None, post=post)) == []


def test_income_bare_symbol_validation():
    with pytest.raises(SidecarError) as excinfo:
        list(fe_cmd.income("^BVSP", None, post=PostRecorder(FakeAjax({}))))
    assert excinfo.value.code == "Usage.Invalid"


# ----- quotations -----

def test_quotations_decodes_double_serialized_and_maps():
    payload_items = [
        {"date": "26/08/21 00:00", "price": "98,50"},
        {"date": "27/08/21 00:00", "price": "99,10"},
    ]
    post = PostRecorder(
        FakeAjax({"success": True, "data": [{"quotations": json.dumps(payload_items)}]})
    )

    records = {r["date"]: r for r, _ in fe_cmd.quotations("KNCR11", None, post=post)}

    assert records["2021-08-26"] == {
        "ticker": "KNCR11",
        "date": "2021-08-26",
        "open": 98.5,
        "high": 98.5,
        "low": 98.5,
        "close": 98.5,
        "adj_close": 98.5,
        "volume": 0,
    }


def test_quotations_dedupes_by_date_and_sort():
    payload_items = [
        {"date": "26/08/21 00:00", "price": "10,00"},
        {"date": "26/08/21 00:00", "price": "11,00"},  # last write wins
    ]
    post = PostRecorder(
        FakeAjax({"success": True, "data": [{"quotations": json.dumps(payload_items)}]})
    )

    records = [r for r, _ in fe_cmd.quotations("KNCR11", None, post=post)]

    assert len(records) == 1
    assert records[0]["close"] == 11.0


# ----- patrimonials -----

def test_patrimonials_maps():
    post = PostRecorder(
        FakeAjax(
            {
                "success": True,
                "data": [
                    {"data_base": "07/2026", "patrimonio": "96,12"},
                    {"data_base": "06/2026", "patrimonio": "95,80"},
                ],
            }
        )
    )

    records = [r for r, _ in fe_cmd.patrimonials("KNCR11", None, post=post)]

    metrics = {r["period"]: r for r in records}
    assert metrics["2026-06"]["metric"] == "equity_per_share"
    assert metrics["2026-07"]["ret"] == 96.12


# ----- nonce discovery path -----

class GetRecorder:
    def __init__(self, response: FakePage) -> None:
        self.response = response
        self.calls: list[str] = []

    def __call__(self, url: str, *, timeout: float) -> FakePage:
        self.calls.append(url)
        return self.response


def test_income_without_injected_post_discovers_nonce_from_page(monkeypatch):
    # Patch _default_post to avoid a real session being spawned in the discovery path.
    def fake_post_factory() -> PostRecorder:  # type: ignore[no-redef]
        return PostRecorder(
            FakeAjax({"success": True, "data": [{"data_base": "01/01/2024", "valor": "1,00"}]})
        )

    monkeypatch.setattr(fe_cmd, "_default_post", fake_post_factory)
    get = GetRecorder(FakePage(html_with_nonce("funds-get-income", "abcd1234ef")))

    records = [r for r, _ in fe_cmd.income("KNCR11", None, get=get, post=None)]

    assert get.calls[0].endswith("/funds/KNCR11")
    assert len(records) == 1


def test_income_without_nonce_is_parse_error(monkeypatch):
    monkeypatch.setattr(fe_cmd, "_default_post", lambda: PostRecorder(FakeAjax({})))  # type: ignore[no-redef]
    get = GetRecorder(FakePage("<html>no nonce</html>"))

    with pytest.raises(SidecarError) as excinfo:
        list(fe_cmd.income("KNCR11", None, get=get, post=None))

    assert excinfo.value.code == "Parse.Invalid"


def test_quotation_need_fixture_roundtrip(tmp_path):
    fixture = write_fixture(
        tmp_path / "q.ndjson",
        [{"ticker": "KNCR11", "date": "2021-08-25", "open": 99, "high": 99, "low": 99, "close": 99, "adj_close": 99, "volume": 0}],
    )
    records = [r for r, _ in fe_cmd.quotations("KNCR11", fixture, post=PostRecorder(FakeAjax({})))]
    assert records[0]["date"] == "2021-08-25"
