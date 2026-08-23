"""``sidecar fetch`` - generic curl_cffi transport, fully offline (fake HTTP layer)."""

from __future__ import annotations

import base64
import json
from typing import Any

import pytest
from sidecar import fetch_cmd
from sidecar.errors import SidecarError


class FakeResponse:
    def __init__(
        self,
        status_code: int = 200,
        content: bytes = b"",
        text: str | None = None,
    ) -> None:
        self.status_code = status_code
        self._content = content
        self._text = text

    @property
    def text(self) -> str:
        if self._text is None:
            self._text = self._content.decode("utf-8", errors="replace")
        return self._text

    @property
    def content(self) -> bytes:
        return self._content


class Recorder:
    """Fake transport capturing requests and replaying canned responses."""

    def __init__(self, responses: list[FakeResponse] | None = None) -> None:
        self.responses = list(responses or [FakeResponse(status_code=200, text="ok")])
        self.calls: list[dict[str, Any]] = []

    def __call__(
        self,
        url: str,
        *,
        method: str,
        headers: dict[str, str],
        data: bytes | None,
        timeout: float,
    ) -> FakeResponse:
        self.calls.append(
            {
                "url": url,
                "method": method,
                "headers": dict(headers),
                "data": data,
                "timeout": timeout,
            }
        )
        return self.responses.pop(0)


def test_get_returns_body_text_and_records_defaults():
    transport = Recorder([FakeResponse(text="<html>ok</html>")])

    body = fetch_cmd.fetch("https://x.test/a/", do_fetch=transport)

    assert body == "<html>ok</html>"
    call = transport.calls[0]
    assert call["url"] == "https://x.test/a/"
    assert call["method"] == "GET"
    assert call["headers"] == {}
    assert call["data"] is None


def test_post_data_and_headers_forwarded():
    transport = Recorder()

    fetch_cmd.fetch(
        "https://x.test/history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009",
        method="POST",
        headers=["Accept: application/json", "X-A:  1"],
        data='{"fundo":"BRBOVVCTF009"}',
        timeout=15.0,
        do_fetch=transport,
    )

    call = transport.calls[0]
    assert call["method"] == "POST"
    assert call["headers"] == {"Accept": "application/json", "X-A": "1"}
    assert call["data"] == '{"fundo":"BRBOVVCTF009"}'.encode("utf-8")
    assert call["timeout"] == 15.0


def test_header_without_colon_is_usage_error():
    with pytest.raises(SidecarError) as excinfo:
        fetch_cmd.fetch("https://x.test/", headers=["broken-header"], do_fetch=Recorder())

    assert excinfo.value.code == "Usage.Invalid"
    assert excinfo.value.exit_code == 2


def test_method_restricted_to_get_post():
    with pytest.raises(SidecarError) as excinfo:
        fetch_cmd.fetch("https://x.test/", method="PUT", do_fetch=Recorder())

    assert excinfo.value.code == "Usage.Invalid"


def test_403_maps_to_scrape_wafblocked_with_status_detail():
    transport = Recorder([FakeResponse(status_code=403)])

    with pytest.raises(SidecarError) as excinfo:
        fetch_cmd.fetch("https://waf.test/a/", do_fetch=transport)

    assert excinfo.value.code == "Scrape.WafBlocked"
    assert excinfo.value.exit_code == 3
    assert excinfo.value.details == {"status": 403}


def test_other_http_error_maps_to_fetch_failed_with_status():
    transport = Recorder([FakeResponse(status_code=500)])

    with pytest.raises(SidecarError) as excinfo:
        fetch_cmd.fetch("https://x.test/a/", do_fetch=transport)

    assert excinfo.value.code == "Fetch.Failed"
    assert excinfo.value.details == {"status": 500}


def test_connection_error_maps_to_fetch_failed_without_status():
    def broken(url: str, **kwargs: Any) -> FakeResponse:
        raise RuntimeError("connection reset")

    with pytest.raises(SidecarError) as excinfo:
        fetch_cmd.fetch("https://down.test/", do_fetch=broken)

    assert excinfo.value.code == "Fetch.Failed"
    assert excinfo.value.details is None  # no HTTP status to report


def test_b64_roundtrips_binary_payload_exactly():
    raw = b"PK\x03\x04\xff\x00\xfebinary\xffpayload"
    transport = Recorder([FakeResponse(content=raw)])

    body = fetch_cmd.fetch("https://x.test/f.xlsx", binary64=True, do_fetch=transport)

    assert base64.b64decode(body) == raw


def test_cli_run_writes_body_plus_newline_to_stdout(monkeypatch, capsys):
    class Args:
        url = "https://x.test/a/"
        method = "GET"
        header = []
        data = None
        timeout_s = 10.0
        b64 = False

    import io

    monkeypatch.setattr(
        fetch_cmd, "_default_fetch", Recorder([FakeResponse(text="body-text")])
    )
    out = io.StringIO()

    exit_code = fetch_cmd.run(Args(), out)

    assert exit_code == 0
    captured = capsys.readouterr()
    assert out.getvalue() == "body-text\n"
    assert "[sidecar]" in captured.err  # logs stay on stderr


def test_cli_envelope_carries_status_on_waf_block(monkeypatch, capsys):
    monkeypatch.setattr(
        fetch_cmd,
        "_default_fetch",
        Recorder([FakeResponse(status_code=403)]),
    )

    from sidecar import cli

    exit_code = cli.main(["fetch", "--url", "https://waf.test/bovv11/composicao/"])

    assert exit_code == 3
    captured = capsys.readouterr()
    assert captured.out == ""  # stdout stays pure body-or-empty
    envelope = json.loads(captured.err.strip().splitlines()[-1])
    assert envelope == {
        "error": {
            "code": "Scrape.WafBlocked",
            "message": "WAF blocked GET https://waf.test/bovv11/composicao/ (HTTP 403)",
            "status": 403,
        }
    }
