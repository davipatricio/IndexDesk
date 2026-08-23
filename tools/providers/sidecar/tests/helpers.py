"""Shared helpers for offline tests."""

from __future__ import annotations

import json
from typing import Any


def quote(
    ticker: str = "PETR4.SA",
    date: str = "2026-08-21",
    open_: float = 30.0,
    high: float = 31.0,
    low: float = 29.5,
    close: float = 30.7,
    adj_close: float = 30.7,
    volume: int = 1_000,
) -> dict[str, Any]:
    return {
        "ticker": ticker,
        "date": date,
        "open": open_,
        "high": high,
        "low": low,
        "close": close,
        "adj_close": adj_close,
        "volume": volume,
    }


def dividend(ticker: str = "PETR4.SA", date: str = "2026-08-01", rate: float = 0.52, type_: str = "DIVIDEND"):
    return {"ticker": ticker, "date": date, "rate": rate, "type": type_}


def write_fixture(path, records: list[dict[str, Any]]) -> str:
    path.write_text("".join(json.dumps(r) + "\n" for r in records), encoding="utf-8")
    return str(path)
