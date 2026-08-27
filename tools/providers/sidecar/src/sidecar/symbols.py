"""Ticker normalization between the .NET caller and each provider library.

Measured facts (Fase 0):
- yfinance needs the ``.SA`` suffix for B3 tickers (bare ``BOVA11`` = 404).
- tv-scraper wants ``EXCHANGE:SYMBOL``; a bare B3 ticker defaults to BMFBOVESPA.

Symbols containing ``.``, ``^`` or ``=`` are passed through untouched by
``yf_symbol`` so benchmarks like ``^BVSP`` and FX pairs like ``USDBRL=X``
keep working.
"""

from __future__ import annotations

from sidecar.errors import usage_error

DEFAULT_TV_EXCHANGE = "BMFBOVESPA"


def yf_symbol(raw: str) -> str:
    """Normalize *raw* for yfinance, appending ``.SA`` when appropriate."""
    symbol = raw.strip().upper()
    if not symbol:
        raise usage_error("--symbol must not be empty")
    if any(ch in symbol for ch in ".^="):
        return symbol
    return f"{symbol}.SA"


def tv_symbol(raw: str) -> tuple[str, str]:
    """Split *raw* into ``(exchange, symbol)`` for tv-scraper.

    Accepts ``BMFBOVESPA:BOVA11`` or a bare B3 ticker (``BOVA11``), which is
    prefixed with the default exchange.
    """
    text = raw.strip().upper()
    if not text:
        raise usage_error("--symbol must not be empty")
    if ":" in text:
        exchange, _, symbol = text.partition(":")
        if not exchange or not symbol:
            raise usage_error(f"malformed TV symbol {raw!r} (want EXCHANGE:SYMBOL)")
        return exchange, symbol
    return DEFAULT_TV_EXCHANGE, text


def im_symbol(raw: str) -> str:
    """Normalize *raw* for the InfoMoney B3 API (bare uppercase ticker).

    The API only serves B3 tickers (``MGLU3``, ``PETR4``); benchmarks and
    FX pairs (``^BVSP``, ``USDBRL=X``) have no equivalent there.
    """
    symbol = raw.strip().upper()
    if not symbol:
        raise usage_error("--symbol must not be empty")
    if any(ch in symbol for ch in ":^=.") or not symbol.isalnum():
        raise usage_error(
            f"InfoMoney serves bare B3 tickers only; {raw!r} is not supported"
        )
    return symbol


def bare_symbol(raw: str) -> str:
    """Normalize *raw* to a bare uppercase B3 ticker (shared by B3 sites:
    FundsExplorer, ClubeFII, Investidor10, ...). Rejects benchmark/FX-style
    symbols, which those sites do not serve."""
    return im_symbol(raw)
