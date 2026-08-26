"""``sidecar b3 ...`` - B3 listed-company/FII catalogs via curl_cffi.

Measured facts (recon 26/08/2026, live):

- The public pages ``www.b3.com.br/.../empresas-listadas.htm`` and
  ``.../fiis-listados/`` are SPA shells that embed Angular apps hosted at
  ``sistemaswebb3-listados.b3.com.br`` (``listedCompaniesPage/`` and
  ``fundsListedPage/FII``).
- Both apps read JSON from GET proxies whose single path segment is
  ``base64(JSON.stringify(filter))`` — exactly what the bundles do with
  ``btoa(...)``:

  - ``GET /listedCompaniesProxy/CompanyCall/GetInitialCompanies/{b64}``
    filter: ``{"codeCategoryBVMF":-1,"company":"","language":"pt-br"}``
    → ~3.5k companies: codeCVM, issuingCompany, tradingName, cnpj,
    marketIndicator, dateListing.
  - ``GET /fundsListedProxy/Search/GetListFunds/{b64}``
    filter: ``{"cnpj":"","keyword":"","language":"pt-br","typeFund":"FII"}``
    → ~530 funds: acronym (ticker), fundName.

- Akamai sits in front: a warm-up GET of the app page sets session cookies
  (``TS*``, ``dtCookie``, ``__cf_bm``) that the proxy calls require. Without
  it the API answers HTTP 200 with an **empty body**, so the warm-up is part
  of the contract here. TLS fingerprint must look like Chrome
  (``impersonate="chrome"``), same as InfoMoney.
- Pagination envelope: ``{"page":{"pageNumber","pageSize","totalRecords",
  "totalPages"},"results":[...]}``.
"""

from __future__ import annotations

import base64
import json
from typing import Any, Iterator, Protocol

from sidecar import ndjson
from sidecar.errors import SidecarError, fetch_error, parse_error, usage_error
from sidecar.schema import COMPANY_KEYS, make_company, make_fund

COMPANIES_APP_URL = (
    "https://sistemaswebb3-listados.b3.com.br/listedCompaniesPage/?language=pt-br"
)
FUNDS_APP_URL = "https://sistemaswebb3-listados.b3.com.br/fundsListedPage/FII"
COMPANIES_API_URL = (
    "https://sistemaswebb3-listados.b3.com.br"
    "/listedCompaniesProxy/CompanyCall/GetInitialCompanies/"
)
FUNDS_API_URL = (
    "https://sistemaswebb3-listados.b3.com.br/fundsListedProxy/Search/GetListFunds/"
)

DEFAULT_PAGE_SIZE = 120
MAX_PAGES = 100  # safety stop: 100 x 120 = 12k records ceiling per invocation
REQUEST_TIMEOUT_SECONDS = 30.0


class ResponseLike(Protocol):
    """Minimal surface of a ``curl_cffi`` response used here."""

    status_code: int

    def json(self) -> Any: ...


class HttpGet(Protocol):
    """Transport seam so offline tests can fake the HTTP layer."""

    def __call__(self, url: str, *, timeout: float) -> ResponseLike: ...


def _chrome_session_get() -> HttpGet:
    """Build a GET bound to one curl_cffi session (cookies survive across calls)."""
    from curl_cffi import requests as curl_requests

    session = curl_requests.Session(impersonate="chrome")

    def get(url: str, *, timeout: float) -> ResponseLike:
        return session.get(url, timeout=timeout)

    return get


def _error_for_status(response: ResponseLike, path: str) -> None:
    if response.status_code == 403:
        # Akamai block page: TLS fingerprint rejected before any app logic ran.
        raise SidecarError("Scrape.WafBlocked", f"Akamai blocked {path} (HTTP 403)", 3)
    if response.status_code != 200:
        raise fetch_error(f"B3 returned HTTP {response.status_code} for {path}")


def _decode_json(response: ResponseLike, path: str) -> dict[str, Any]:
    try:
        payload = response.json()
    except Exception as exc:
        raise parse_error(f"B3 {path}: invalid JSON ({exc})") from exc
    if not isinstance(payload, dict):
        raise parse_error(f"B3 {path}: expected a JSON object")
    return payload


def _encode_filter(filter_obj: dict[str, Any]) -> str:
    """Mirror the Angular bundles' ``btoa(JSON.stringify(filter))`` path token."""
    raw = json.dumps(filter_obj).encode("utf-8")
    return base64.b64encode(raw).decode("ascii")


def _paged_results(
    get: HttpGet,
    base_url: str,
    filter_obj: dict[str, Any],
    *,
    page_size: int,
    max_pages: int,
    max_records: int | None = None,
) -> Iterator[dict[str, Any]]:
    """Walk ``page.totalRecords`` yielding raw result objects."""
    collected = 0
    for page in range(1, max_pages + 1):
        page_filter = dict(filter_obj)
        page_filter["pageNumber"] = page
        page_filter["pageSize"] = page_size
        response = get(
            f"{base_url}{_encode_filter(page_filter)}",
            timeout=REQUEST_TIMEOUT_SECONDS,
        )
        _error_for_status(response, base_url)
        payload = _decode_json(response, "catalog")
        results = payload.get("results") or []
        if not isinstance(results, list):
            raise parse_error("B3 catalog: 'results' must be a list")
        for row in results:
            if max_records is not None and collected >= max_records:
                return
            collected += 1
            yield row
        total = (payload.get("page") or {}).get("totalRecords")
        if not results or (isinstance(total, int) and collected >= total):
            return


def companies(
    fixture: str | None,
    *,
    get: HttpGet | None = None,
    page_size: int = DEFAULT_PAGE_SIZE,
    max_records: int | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar b3 companies``."""
    if page_size <= 0:
        raise usage_error("--page-size must be a positive integer")
    if max_records is not None and max_records <= 0:
        raise usage_error("--max-records must be a positive integer")

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "company")
        return

    if get is None:
        get = _chrome_session_get()

    with ndjson.stdout_guard():
        ndjson.log(f"b3 companies page_size={page_size}")
        warmup = get(COMPANIES_APP_URL, timeout=REQUEST_TIMEOUT_SECONDS)
        _error_for_status(warmup, COMPANIES_APP_URL)
        rows = _paged_results(
            get,
            COMPANIES_API_URL,
            {"codeCategoryBVMF": -1, "company": "", "language": "pt-br"},
            page_size=page_size,
            max_pages=MAX_PAGES,
            max_records=max_records,
        )
        records = []
        for raw in rows:
            issuing = str(raw.get("issuingCompany") or "")
            if not issuing:
                raise parse_error(
                    f"B3 catalog company row without issuingCompany: {str(raw)[:80]}"
                )
            records.append(
                make_company(
                    cnpj=str(raw.get("cnpj") or ""),
                    code_cvm=str(raw.get("codeCVM") or ""),
                    issuing_company=issuing,
                    trading_name=str(raw.get("tradingName") or ""),
                    market_indicator=str(raw.get("marketIndicator") or ""),
                    date_listing=str(raw.get("dateListing") or ""),
                )
            )

    for record in records:
        yield record, None


def fiis(
    fixture: str | None,
    *,
    fund_type: str = "FII",
    get: HttpGet | None = None,
    page_size: int = DEFAULT_PAGE_SIZE,
    max_records: int | None = None,
) -> Iterator[tuple[dict[str, Any], str | None]]:
    """Command body for ``sidecar b3 fiis`` (empty list emits nothing)."""
    if page_size <= 0:
        raise usage_error("--page-size must be a positive integer")
    if max_records is not None and max_records <= 0:
        raise usage_error("--max-records must be a positive integer")

    if fixture is not None:
        yield from ndjson.fixture_lines(fixture, "fund")
        return

    if get is None:
        get = _chrome_session_get()

    fund_type = fund_type.strip().upper()
    if not fund_type:
        raise usage_error("--type must be a non-empty string")

    with ndjson.stdout_guard():
        ndjson.log(f"b3 fiis type={fund_type} page_size={page_size}")
        warmup = get(FUNDS_APP_URL, timeout=REQUEST_TIMEOUT_SECONDS)
        _error_for_status(warmup, FUNDS_APP_URL)
        rows = _paged_results(
            get,
            FUNDS_API_URL,
            {"cnpj": "", "keyword": "", "language": "pt-br", "typeFund": fund_type},
            page_size=page_size,
            max_pages=MAX_PAGES,
            max_records=max_records,
        )
        records = []
        for raw in rows:
            acronym = str(raw.get("acronym") or "").strip().upper()
            name = str(raw.get("fundName") or "")
            if not acronym:
                raise parse_error(
                    f"B3 catalog fund row without acronym: {str(raw)[:80]}"
                )
            records.append(
                make_fund(ticker=acronym, name=name, fund_type=fund_type)
            )

    for record in records:
        yield record, None
