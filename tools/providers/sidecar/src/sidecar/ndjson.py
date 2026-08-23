"""NDJSON input/output discipline: stdout carries data only, stderr carries logs.

The .NET worker parses stdout line by line, so nothing else may ever touch it:
during provider fetches ``sys.stdout`` is swapped for stderr (see
:func:`stdout_guard`) so stray library ``print()`` output cannot corrupt the
stream. Errors are reported as a single JSON line on **stderr**:

    {"error":{"code":"Fetch.Failed","message":"..."}}

``--fixture FILE`` mode reads a file of ready-made NDJSON lines, validates each
line against the schema (invalid line = exit 4) and emits the original lines
verbatim - letting C# tests run without network.
"""

from __future__ import annotations

import json
import sys
from contextlib import redirect_stdout
from typing import Any, Iterable, Iterator, TextIO

from sidecar.errors import SidecarError, parse_error
from sidecar.schema import validate_record


def emit(record: dict[str, Any], out: TextIO) -> None:
    """Write one record as a compact NDJSON line."""
    out.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")))
    out.write("\n")


def emit_all(records: Iterable[dict[str, Any]], out: TextIO) -> int:
    """Write every record; returns the number of lines written."""
    count = 0
    for record in records:
        emit(record, out)
        count += 1
    return count


def emit_error(
    code: str,
    message: str,
    err: TextIO | None = None,
    details: dict[str, Any] | None = None,
) -> None:
    """Print the contract error envelope to stderr (never stdout).

    ``details`` adds provider-specific fields after ``message`` (e.g.
    ``{"status": 403}`` from ``sidecar fetch``); the C# runner keys off
    ``error.code`` so extra fields stay backward compatible.
    """
    stream = err if err is not None else sys.stderr
    payload: dict[str, Any] = {"code": code, "message": message}
    if details:
        payload.update(details)
    stream.write(json.dumps({"error": payload}, ensure_ascii=False) + "\n")
    stream.flush()


def log(message: str, err: TextIO | None = None) -> None:
    """Human-readable log line to stderr."""
    stream = err if err is not None else sys.stderr
    stream.write(f"[sidecar] {message}\n")
    stream.flush()


class stdout_guard:
    """Redirect anything written to ``sys.stdout`` to stderr inside the block.

    The real stdout handle is returned so the caller can still emit NDJSON
    while the guard is active.
    """

    def __init__(self) -> None:
        self._real_stdout: TextIO | None = None
        self._redirect: redirect_stdout | None = None

    def __enter__(self) -> TextIO:
        self._real_stdout = sys.stdout
        self._redirect = redirect_stdout(sys.stderr)
        self._redirect.__enter__()
        return self._real_stdout

    def __exit__(self, *exc_info: object) -> None:
        if self._redirect is not None:
            self._redirect.__exit__(*exc_info)  # restores the original sys.stdout


def fixture_lines(path: str, kind: str) -> Iterator[tuple[dict[str, Any], str]]:
    """Validated ``(record, raw_line)`` pairs so fixtures are emitted verbatim.

    Raises :class:`SidecarError` (exit 4) when the file cannot be read, a line
    is not valid JSON, or a line violates the schema for *kind*.
    """
    try:
        handle = open(path, "r", encoding="utf-8")
    except OSError as exc:
        raise parse_error(f"cannot read fixture {path!r}: {exc}") from exc

    with handle:
        for lineno, raw in enumerate(handle, start=1):
            line = raw.strip()
            if not line:
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError as exc:
                raise parse_error(f"{path}:{lineno}: invalid JSON ({exc})") from exc
            try:
                validate_record(record, kind)
            except SidecarError as exc:
                raise parse_error(f"{path}:{lineno}: {exc.message}") from exc
            yield record, line
