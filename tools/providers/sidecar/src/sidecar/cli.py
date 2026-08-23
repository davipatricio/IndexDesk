"""Argument parsing and process-level contract enforcement.

Layout::

    sidecar yf quotes    --symbol PETR4.SA [--start YYYY-MM-DD] [--end YYYY-MM-DD] [--fixture FILE]
    sidecar yf dividends --symbol PETR4.SA [--fixture FILE]
    sidecar tv history   --symbol BMFBOVESPA:BOVA11 [--interval 1d] [--bars N]
                         [--cookie COOKIE] [--fixture FILE]
    sidecar im quotes    --symbol MGLU3 [--bars N] [--fixture FILE]
    sidecar im dividends --symbol MGLU3 [--fixture FILE]
    sidecar fetch        --url URL [--method GET|POST] [--data BODY]
                         [--header "K: V" ...] [--timeout-s N] [--b64]

Exit codes: 0 ok, 2 usage, 3 fetch failure, 4 parse error. Errors go to stderr
as ``{"error":{"code":...,"message":...}}``; stdout stays pure NDJSON.
"""

from __future__ import annotations

import argparse
import sys
from collections.abc import Sequence

from sidecar import fetch_cmd, im_cmd, ndjson, tv_cmd, yf_cmd
from sidecar.errors import EXIT_OK, EXIT_PARSE, SidecarError


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="sidecar",
        description="IndexDesk provider sidecar: emits NDJSON on stdout (logs on stderr).",
    )
    commands = parser.add_subparsers(dest="command", required=True)

    yf = commands.add_parser("yf", help="Yahoo Finance (yfinance)")
    yf_commands = yf.add_subparsers(dest="yf_command", required=True)

    p_quotes = yf_commands.add_parser("quotes", help="daily OHLCV + adj_close")
    p_quotes.add_argument("--symbol", required=True, help="e.g. PETR4.SA or bare PETR4")
    p_quotes.add_argument("--start", metavar="YYYY-MM-DD")
    p_quotes.add_argument("--end", metavar="YYYY-MM-DD")
    p_quotes.add_argument("--fixture", metavar="FILE", help="emit this NDJSON file instead of fetching")

    p_div = yf_commands.add_parser("dividends", help="cash dividends history")
    p_div.add_argument("--symbol", required=True)
    p_div.add_argument("--fixture", metavar="FILE")

    tv = commands.add_parser("tv", help="TradingView (tv-scraper)")
    tv_commands = tv.add_subparsers(dest="tv_command", required=True)

    p_hist = tv_commands.add_parser("history", help="OHLCV candles")
    p_hist.add_argument("--symbol", required=True, help="BMFBOVESPA:BOVA11 or bare BOVA11")
    p_hist.add_argument("--interval", default="1d", help="1m..1M (default 1d)")
    p_hist.add_argument("--bars", type=int, default=1000, help="number of candles (default 1000)")
    p_hist.add_argument("--cookie", default=None, help="TradingView session cookie (optional)")
    p_hist.add_argument("--fixture", metavar="FILE")

    im = commands.add_parser("im", help="InfoMoney (XP Inc) market-data API")
    im_commands = im.add_subparsers(dest="im_command", required=True)

    p_im_quotes = im_commands.add_parser("quotes", help="daily OHLCV history (unadjusted)")
    p_im_quotes.add_argument("--symbol", required=True, help="bare B3 ticker, e.g. MGLU3")
    p_im_quotes.add_argument(
        "--bars", type=int, default=1000, help="approximate number of daily bars (default 1000)"
    )
    p_im_quotes.add_argument("--fixture", metavar="FILE")

    p_im_div = im_commands.add_parser("dividends", help="cash dividends/events history")
    p_im_div.add_argument("--symbol", required=True)
    p_im_div.add_argument("--fixture", metavar="FILE")

    p_fetch = commands.add_parser(
        "fetch",
        help="generic HTTP GET/POST through curl_cffi (WAF-safe transport)",
        description=(
            "Fetch one URL with curl_cffi impersonate=chrome and print the raw "
            "response body on stdout (text by default; --b64 for binary)."
        ),
    )
    p_fetch.add_argument("--url", required=True)
    p_fetch.add_argument("--method", default="GET", choices=["GET", "POST"])
    p_fetch.add_argument("--data", default=None, help="request body (utf-8 text)")
    p_fetch.add_argument(
        "--header",
        action="append",
        default=[],
        metavar='"Name: Value"',
        help="extra request header; repeatable",
    )
    p_fetch.add_argument(
        "--timeout-s",
        dest="timeout_s",
        type=float,
        default=fetch_cmd.DEFAULT_TIMEOUT_SECONDS,
        help=f"per-request timeout in seconds (default {fetch_cmd.DEFAULT_TIMEOUT_SECONDS:.0f})",
    )
    p_fetch.add_argument(
        "--b64",
        action="store_true",
        help="emit the response body as base64 (binary payloads such as XLSX)",
    )

    return parser


def _write_pairs(pairs, out) -> int:
    """Write (record, raw_line) pairs; raw_line wins so fixtures stay verbatim."""
    materialized = list(pairs)  # validate/fetch fully before stdout sees a byte
    for record, raw in materialized:
        if raw is not None:
            out.write(raw + "\n")
        else:
            ndjson.emit(record, out)
    return len(materialized)


def run(args: argparse.Namespace, out) -> int:
    if args.command == "fetch":
        # Raw-body command: stdout is the response body, not NDJSON pairs.
        return fetch_cmd.run(args, out)

    if args.command == "yf" and args.yf_command == "quotes":
        pairs = yf_cmd.quotes(args.symbol, args.start, args.end, args.fixture)
    elif args.command == "yf" and args.yf_command == "dividends":
        pairs = yf_cmd.dividends(args.symbol, args.fixture)
    elif args.command == "tv" and args.tv_command == "history":
        pairs = tv_cmd.history(args.symbol, args.interval, args.bars, args.fixture, args.cookie)
    elif args.command == "im" and args.im_command == "quotes":
        pairs = im_cmd.quotes(args.symbol, args.bars, args.fixture)
    elif args.command == "im" and args.im_command == "dividends":
        pairs = im_cmd.dividends(args.symbol, args.fixture)
    else:  # pragma: no cover - argparse enforces the combinations
        raise SidecarError("Usage.Invalid", f"unknown command {args.command}", 2)
    count = _write_pairs(pairs, out)
    ndjson.log(f"emitted {count} lines")
    return EXIT_OK


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    out = sys.stdout
    try:
        return run(args, out)
    except SidecarError as exc:
        ndjson.emit_error(exc.code, exc.message, details=exc.details)
        return exc.exit_code
    except BrokenPipeError:
        ndjson.emit_error("Fetch.Failed", "stdout closed by consumer")
        return EXIT_PARSE
    except KeyboardInterrupt:  # pragma: no cover
        ndjson.emit_error("Fetch.Failed", "interrupted")
        return 3
    except Exception as exc:  # unexpected bug: still honor the contract shape
        ndjson.emit_error("Fetch.Failed", f"unexpected {type(exc).__name__}: {exc}")
        return 3


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
