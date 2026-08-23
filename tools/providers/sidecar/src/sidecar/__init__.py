"""sidecar - provider fetch CLI emitting versioned NDJSON on stdout.

Contract (see README.md for the full document):

- One JSON object per line on **stdout**; all logs/warnings go to **stderr**.
- Quote line:      {"ticker","date","open","high","low","close","adj_close","volume"}
- Dividend line:   {"ticker","date","rate","type"}
- Dates are ISO-8601 ``YYYY-MM-DD``; every numeric field is a JSON number.
- Exit codes: 0 ok, 2 spawn/usage error, 3 fetch failure, 4 parse error.
- Errors print ``{"error":{"code":"...","message":"..."}}`` to **stderr**, never stdout.
"""

__version__ = "0.1.0"
