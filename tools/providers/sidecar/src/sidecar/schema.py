"""Quote/dividend record schemas and validation for the NDJSON contract.

Contract v1 (one JSON object per line on stdout):

- Quote:    ``{"ticker","date","open","high","low","close","adj_close","volume"}``
- Dividend: ``{"ticker","date","rate","type"}``

Contract v2 additions (catalog records, ``sidecar b3 ...``):

- Company: ``{"cnpj","code_cvm","issuing_company","trading_name",
  "market_indicator","date_listing"}`` — dates stay verbatim B3 format
  (``dd/mm/yyyy``); they are metadata, not quote dates.
- Fund:    ``{"ticker","name","fund_type"}``

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
COMPANY_KEYS = frozenset(
    {
        "cnpj",
        "code_cvm",
        "issuing_company",
        "trading_name",
        "market_indicator",
        "date_listing",
    }
)
FUND_KEYS = frozenset({"ticker", "name", "fund_type"})

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


def make_company(
    *,
    cnpj: str,
    code_cvm: str,
    issuing_company: str,
    trading_name: str,
    market_indicator: str,
    date_listing: str,
) -> dict[str, Any]:
    """Build a listed-company record with contract-ordered keys."""
    return {
        "cnpj": cnpj,
        "code_cvm": code_cvm,
        "issuing_company": issuing_company,
        "trading_name": trading_name,
        "market_indicator": market_indicator,
        "date_listing": date_listing,
    }


def make_fund(*, ticker: str, name: str, fund_type: str) -> dict[str, Any]:
    """Build a listed-fund record with contract-ordered keys."""
    return {"ticker": ticker, "name": name, "fund_type": fund_type}


def _validate_number(record: dict[str, Any], key: str) -> None:
    value = record[key]
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise parse_error(f"field {key!r} must be a JSON number")
    if not math.isfinite(float(value)):
        raise parse_error(f"field {key!r} must be finite")


def _validate_string(record: dict[str, Any], key: str, *, required: bool = True) -> None:
    value = record[key]
    if not isinstance(value, str):
        raise parse_error(f"field {key!r} must be a string")
    if required and not value:
        raise parse_error(f"field {key!r} must be a non-empty string")


def validate_record(record: Any, kind: str) -> dict[str, Any]:
    """Validate one decoded NDJSON object against the schema for *kind*.

    *kind* is ``"quote"``, ``"dividend"``, ``"company"`` or ``"fund"``.
    Raises :class:`SidecarError` with exit code 4 on any violation; returns
    the record unchanged when valid.
    """
    if not isinstance(record, dict):
        raise parse_error(f"{kind} line must be a JSON object")

    expected: frozenset[str]
    numbers: tuple[str, ...] = ()
    if kind == "quote":
        expected = QUOTE_KEYS
        numbers = ("open", "high", "low", "close", "adj_close", "volume")
    elif kind == "dividend":
        expected = DIVIDEND_KEYS
        numbers = ("rate",)
    elif kind == "company":
        expected = COMPANY_KEYS
    elif kind == "fund":
        expected = FUND_KEYS
    else:
        raise parse_error(f"unknown record kind {kind!r}")

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

    if kind in ("quote", "dividend"):
        if not isinstance(record["ticker"], str) or not record["ticker"]:
            raise parse_error("field 'ticker' must be a non-empty string")
        if not isinstance(record["date"], str) or not DATE_RE.match(record["date"]):
            raise parse_error("field 'date' must be an ISO-8601 YYYY-MM-DD string")
        try:
            _date.fromisoformat(record["date"])
        except ValueError as exc:
            raise parse_error(f"invalid calendar date {record['date']!r}") from exc

        for key in numbers:
            _validate_number(record, key)

        if kind == "dividend":
            if not isinstance(record["type"], str) or not record["type"]:
                raise parse_error("field 'type' must be a non-empty string")
    elif kind == "company":
        for key in sorted(COMPANY_KEYS):
            _validate_string(record, key, required=key == "issuing_company")
    else:  # fund
        for key in sorted(FUND_KEYS):
            _validate_string(record, key)

    return record
