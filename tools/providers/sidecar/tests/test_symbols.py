"""Symbol mapping: yfinance suffix and TradingView exchange prefix."""

from __future__ import annotations

import pytest

from sidecar.errors import SidecarError
from sidecar.symbols import DEFAULT_TV_EXCHANGE, tv_symbol, yf_symbol


def test_yf_appends_sa_to_bare_b3_ticker():
    assert yf_symbol("PETR4") == "PETR4.SA"
    assert yf_symbol("bova11") == "BOVA11.SA"


def test_yf_passthrough_when_dotted():
    assert yf_symbol("PETR4.SA") == "PETR4.SA"


def test_yf_passthrough_benchmarks_and_fx():
    # ^ and = symbols must never receive .SA
    assert yf_symbol("^BVSP") == "^BVSP"
    assert yf_symbol("USDBRL=X") == "USDBRL=X"
    assert yf_symbol("GC=F") == "GC=F"


def test_yf_empty_is_usage_error():
    with pytest.raises(SidecarError) as excinfo:
        yf_symbol("  ")
    assert excinfo.value.exit_code == 2


def test_tv_splits_exchange_symbol():
    assert tv_symbol("BMFBOVESPA:BOVA11") == ("BMFBOVESPA", "BOVA11")
    assert tv_symbol("nasdaq:aapl") == ("NASDAQ", "AAPL")


def test_tv_bare_ticker_defaults_to_bmfbovespa():
    exchange, symbol = tv_symbol("bova11")
    assert exchange == DEFAULT_TV_EXCHANGE == "BMFBOVESPA"
    assert symbol == "BOVA11"


def test_tv_malformed_pair_is_usage_error():
    with pytest.raises(SidecarError) as excinfo:
        tv_symbol("BMFBOVESPA:")
    assert excinfo.value.exit_code == 2
