"""``sidecar fetch`` - generic HTTP fetch through ``curl_cffi`` (anti-WAF transport).

Measured facts (Fase 3 adendo, 2026-08-23): Akamai TLS-fingerprints non-browser
clients on hosts such as ``www.itnow.com.br`` - plain curl and .NET
``HttpClient`` get 403 "Access Denied" even with complete browser headers,
while ``curl_cffi impersonate="chrome"`` gets 200. This command exposes that
transport generically so .NET feeds can route individual WAF-protected sources
through the sidecar instead of growing one bespoke command per host.

Contract (differs from the NDJSON commands):

- **stdout carries the raw response body**: UTF-8 text by default, base64 of
  the raw bytes with ``--b64`` (binary payloads such as XLSX). One trailing
  newline is appended so line-oriented readers terminate cleanly; parsers
  must treat the whole output as the body.
- **stderr** carries logs plus, on failure, the usual single error envelope -
  extended with the HTTP ``status`` when there is one::

      {"error":{"code":"Scrape.WafBlocked","message":"...","status":403}}

- HTTP 403 maps to ``Scrape.WafBlocked`` (Akamai-style TLS block); any other
  status >= 400, connection errors and timeouts map to ``Fetch.Failed``. Both
  exit 3. Success exits 0 even for a 2xx/3xx body.
"""

from __future__ import annotations

import base64
from typing import Any, Protocol

from sidecar import ndjson
from sidecar.errors import EXIT_FETCH, SidecarError, fetch_error, usage_error

DEFAULT_TIMEOUT_SECONDS = 30.0


class ResponseLike(Protocol):
    """Minimal surface of a ``curl_cffi`` response used here."""

    status_code: int
    text: str
    content: bytes


class HttpFetch(Protocol):
    """Transport seam so offline tests can fake the HTTP layer."""

    def __call__(
        self,
        url: str,
        *,
        method: str,
        headers: dict[str, str],
        data: bytes | None,
        timeout: float,
    ) -> ResponseLike: ...


def _default_fetch(
    url: str,
    *,
    method: str,
    headers: dict[str, str],
    data: bytes | None,
    timeout: float,
) -> ResponseLike:
    from curl_cffi import requests as curl_requests

    return curl_requests.request(
        method,
        url,
        headers=headers or None,
        data=data,
        timeout=timeout,
        impersonate="chrome",  # Akamai WAF: TLS fingerprint must look like Chrome
    )


def parse_header(raw: str) -> tuple[str, str]:
    """Parse a ``Name: Value`` CLI header into its parts (usage error otherwise)."""
    name, sep, value = raw.partition(":")
    if not sep or not name.strip():
        raise usage_error(f"--header expects 'Name: Value', got {raw!r}")
    return name.strip(), value.strip()


def fetch(
    url: str,
    *,
    method: str = "GET",
    headers: list[str] | None = None,
    data: str | bytes | None = None,
    timeout: float = DEFAULT_TIMEOUT_SECONDS,
    binary64: bool = False,
    do_fetch: HttpFetch | None = None,
) -> str:
    """Perform one request and return the body ready for stdout.

    Raises :class:`SidecarError` (exit 3) for connection failures and any HTTP
    status >= 400; the error envelope carries ``status`` when known.
    """
    if method.upper() not in {"GET", "POST"}:
        raise usage_error(f"--method must be GET or POST, got {method!r}")

    header_map: dict[str, str] = {}
    for raw in headers or []:
        name, value = parse_header(raw)
        header_map[name] = value

    payload: bytes | None = None
    if data is not None:
        payload = data.encode("utf-8") if isinstance(data, str) else data

    transport = do_fetch if do_fetch is not None else _default_fetch
    with ndjson.stdout_guard():
        ndjson.log(f"fetch {method.upper()} {url}")
        try:
            response = transport(
                url,
                method=method.upper(),
                headers=header_map,
                data=payload,
                timeout=float(timeout),
            )
        except Exception as exc:
            raise fetch_error(f"{method.upper()} {url} failed: {type(exc).__name__}: {exc}") from exc

        status = response.status_code
        if status == 403:
            # Akamai-style block: TLS fingerprint rejected before content rules.
            raise SidecarError(
                "Scrape.WafBlocked",
                f"WAF blocked {method.upper()} {url} (HTTP 403)",
                EXIT_FETCH,
                details={"status": 403},
            )
        if status >= 400:
            raise fetch_error(f"{method.upper()} {url} returned HTTP {status}", details={"status": status})

        content: bytes = response.content
        body = base64.b64encode(content).decode("ascii") if binary64 else response.text
        ndjson.log(f"fetch ok status={status} bytes={len(content)} b64={binary64}")
        return body


def run(args: Any, out: Any) -> int:
    """CLI body: perform the fetch and write the body (+ newline) to stdout."""
    body = fetch(
        args.url,
        method=args.method,
        headers=args.header,
        data=args.data,
        timeout=args.timeout_s,
        binary64=args.b64,
    )
    out.write(body + "\n")
    return 0
