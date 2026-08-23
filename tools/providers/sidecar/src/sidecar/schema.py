"""Quote/dividend record schemas and validation for the NDJSON contract.

Contract v1 (one JSON object per line on stdout):

- Quote:    ``{"ticker","date","open","high","low","close","adj_close","volume"}``
- Dividend: ``{"ticker","date","rate","type"}``

``date`` is ISO-8601 ``YYYY-MM-DD``; numeric fields are JSON numbers
(int or float, never bool, never NaN/Infinity); ``ticker`` and ``type``
are non-empty strings. Empty output is valid (e.g. a ticker with no
dividends emits zero lines).
"""

from __future__ import annotations

import math
import re
from datetime import date as _date
from typing import Any

from sidecar.errors import parse_error

QUOTE_KEYS = frozenset(
    {"ticker", "date", "open", "high", "low", "close", "adj_close", "volume"}
)
DIVIDEND_KEYS = frozenset({"ticker", "date", "rate", "type"})

DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")


def make_quote(
    *,
    ticker: str,
    date: str,
    open_: float,
    high: float,
    low: float,
    close: float,
    adj_close: float,
    volume: int | float,
) -> dict[str, Any]:
    """Build a quote record with contract-ordered keys."""
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


def make_dividend(*, ticker: str, date: str, rate: float, type_: str) -> dict[str, Any]:
    """Build a dividend record with contract-ordered keys."""
    return {"ticker": ticker, "date": date, "rate": rate, "type": type_}


def _validate_number(record: dict[str, Any], key: str) -> None:
    value = record[key]
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise parse_error(f"field {key!r} must be a JSON number")
    if not math.isfinite(float(value)):
        raise parse_error(f"field {key!r} must be finite")


def validate_record(record: Any, kind: str) -> dict[str, Any]:
    """Validate one decoded NDJSON object against the schema for *kind*.

    *kind* is ``"quote"`` or ``"dividend"``. Raises :class:`SidecarError`
    with exit code 4 on any violation; returns the record unchanged when valid.
    """
    if not isinstance(record, dict):
        raise parse_error(f"{kind} line must be a JSON object")

    expected = QUOTE_KEYS if kind == "quote" else DIVIDEND_KEYS
    actual = set(record)
    if actual != set(expected):
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        detail = []
        if missing:
            detail.append(f"missing {missing}")
        if extra:
            detail.append(f"unexpected {extra}")
        raise parse_error(f"{kind} schema mismatch: {'; '.join(detail)}")

    if not isinstance(record["ticker"], str) or not record["ticker"]:
        raise parse_error("field 'ticker' must be a non-empty string")
    if not isinstance(record["date"], str) or not DATE_RE.match(record["date"]):
        raise parse_error("field 'date' must be an ISO-8601 YYYY-MM-DD string")
    try:
        _date.fromisoformat(record["date"])
    except ValueError as exc:
        raise parse_error(f"invalid calendar date {record['date']!r}") from exc

    numbers = (
        ("open", "high", "low", "close", "adj_close", "volume")
        if kind == "quote"
        else ("rate",)
    )
    for key in numbers:
        _validate_number(record, key)

    if kind == "dividend":
        if not isinstance(record["type"], str) or not record["type"]:
            raise parse_error("field 'type' must be a non-empty string")

    return record
