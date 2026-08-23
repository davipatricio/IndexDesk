"""Error taxonomy mapped to the NDJSON contract exit codes.

0 = ok, 2 = spawn/usage, 3 = fetch failure, 4 = parse error.
"""

from __future__ import annotations

from typing import Any

EXIT_OK = 0
EXIT_USAGE = 2
EXIT_FETCH = 3
EXIT_PARSE = 4


class SidecarError(Exception):
    """An error that maps onto the sidecar contract (code + exit code).

    ``details`` carries extra envelope fields (e.g. ``{"status": 403}`` for
    ``sidecar fetch`` HTTP failures); it is merged into the emitted error
    object after ``message``.
    """

    def __init__(
        self,
        code: str,
        message: str,
        exit_code: int,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.exit_code = exit_code
        self.details = details or None


def usage_error(message: str) -> SidecarError:
    """Bad arguments / unsupported option combination (exit 2)."""
    return SidecarError("Usage.Invalid", message, EXIT_USAGE)


def fetch_error(message: str, details: dict[str, Any] | None = None) -> SidecarError:
    """Provider fetch failed after retries (exit 3)."""
    return SidecarError("Fetch.Failed", message, EXIT_FETCH, details)


def parse_error(message: str) -> SidecarError:
    """Fixture/NDJSON content does not parse or violates the schema (exit 4)."""
    return SidecarError("Parse.Invalid", message, EXIT_PARSE)
