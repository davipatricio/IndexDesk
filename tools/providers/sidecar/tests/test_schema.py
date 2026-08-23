"""Schema validation for the NDJSON contract."""

from __future__ import annotations

import pytest

from helpers import dividend, quote
from sidecar.errors import SidecarError
from sidecar.schema import validate_record


def test_valid_quote_passes():
    assert validate_record(quote(), "quote") == quote()


def test_valid_dividend_passes():
    assert validate_record(dividend(), "dividend") == dividend()


def test_quote_missing_key_rejected():
    broken = quote()
    del broken["adj_close"]
    with pytest.raises(SidecarError) as excinfo:
        validate_record(broken, "quote")
    assert excinfo.value.exit_code == 4
    assert "missing" in excinfo.value.message


def test_quote_extra_key_rejected():
    broken = quote()
    broken["currency"] = "BRL"
    with pytest.raises(SidecarError):
        validate_record(broken, "quote")


def test_bad_date_format_rejected():
    with pytest.raises(SidecarError):
        validate_record(quote(date="21/08/2026"), "quote")


def test_impossible_calendar_date_rejected():
    with pytest.raises(SidecarError):
        validate_record(quote(date="2026-02-30"), "quote")


def test_string_number_rejected():
    broken = quote(close="30.7")
    with pytest.raises(SidecarError):
        validate_record(broken, "quote")


def test_bool_number_rejected():
    # bool is an int subclass in Python; the contract forbids it
    broken = quote(volume=True)
    with pytest.raises(SidecarError):
        validate_record(broken, "quote")


def test_nan_rejected():
    broken = quote(adj_close=float("nan"))
    with pytest.raises(SidecarError):
        validate_record(broken, "quote")


def test_empty_ticker_rejected():
    with pytest.raises(SidecarError):
        validate_record(quote(ticker=""), "quote")


def test_dividend_type_required():
    broken = dividend(type_="")
    with pytest.raises(SidecarError):
        validate_record(broken, "dividend")


def test_non_object_line_rejected():
    with pytest.raises(SidecarError):
        validate_record([1, 2], "quote")
