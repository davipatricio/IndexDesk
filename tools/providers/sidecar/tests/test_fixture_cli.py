"""--fixture mode and CLI-level stdout/stderr discipline (all offline)."""

from __future__ import annotations

import json

from helpers import dividend, quote, write_fixture
from sidecar.cli import main


def _lines(capsys):
    captured = capsys.readouterr()
    return captured.out.splitlines(), captured.err


def test_yf_quotes_fixture_emits_verbatim(tmp_path, capsys):
    records = [quote(date="2026-08-20"), quote(date="2026-08-21", volume=2)]
    fixture = write_fixture(tmp_path / "q.ndjson", records)

    code = main(["yf", "quotes", "--symbol", "PETR4.SA", "--fixture", fixture])

    assert code == 0
    out, err = _lines(capsys)
    assert len(out) == 2
    for line, expected in zip(out, records):
        assert json.loads(line) == expected
    # logs (emitted N lines) go to stderr only
    assert "[sidecar]" in err


def test_tv_history_fixture_roundtrip(tmp_path, capsys):
    records = [quote(ticker="BMFBOVESPA:BOVA11")]
    fixture = write_fixture(tmp_path / "tv.ndjson", records)

    code = main(["tv", "history", "--symbol", "BMFBOVESPA:BOVA11", "--fixture", fixture])

    assert code == 0
    out, _ = _lines(capsys)
    assert [json.loads(line) for line in out] == records


def test_dividends_fixture_empty_file_is_valid_zero_lines(tmp_path, capsys):
    fixture = str(tmp_path / "empty.ndjson")
    (tmp_path / "empty.ndjson").write_text("", encoding="utf-8")

    code = main(["yf", "dividends", "--symbol", "PETR4.SA", "--fixture", fixture])

    assert code == 0
    out, _ = _lines(capsys)
    assert out == []  # empty output is valid NDJSON, not an error


def test_invalid_json_line_exits_4_with_stderr_error(tmp_path, capsys):
    fixture = tmp_path / "bad.ndjson"
    fixture.write_text('{"ticker":"X"}\nnot-json\n', encoding="utf-8")

    code = main(["yf", "dividends", "--symbol", "PETR4.SA", "--fixture", str(fixture)])

    assert code == 4
    out, err = _lines(capsys)
    assert out == []  # nothing on stdout: errors never masquerade as data
    envelope = json.loads(err.strip().splitlines()[-1])
    assert envelope["error"]["code"] == "Parse.Invalid"


def test_schema_violation_in_fixture_exits_4(tmp_path, capsys):
    broken = quote()
    del broken["volume"]
    fixture = write_fixture(tmp_path / "broken.ndjson", [broken])

    code = main(["yf", "quotes", "--symbol", "PETR4", "--fixture", fixture])

    assert code == 4
    out, err = _lines(capsys)
    assert out == []
    assert json.loads(err.strip().splitlines()[-1])["error"]["code"] == "Parse.Invalid"


def test_stdout_stays_pure_ndjson_even_when_logging(tmp_path, capsys):
    records = [dividend()]
    fixture = write_fixture(tmp_path / "d.ndjson", records)

    code = main(["yf", "dividends", "--symbol", "PETR4", "--fixture", fixture])
    out, _ = _lines(capsys)

    assert code == 0
    for line in out:
        json.loads(line)  # every stdout line must parse as JSON
