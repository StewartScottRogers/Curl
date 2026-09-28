# ADR-0060 — `%{referer}`, `%{filename_effective}`, `%{conn_id}` and `%{xfer_id}` come from `Curl.Console`

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-305, 2026-09-27).

## Context

ADR-0043 left these four `-w` variables unknown: they are known to the command line and
the process, not to a handler (ADR-0015, "What the report does not carry"). Measured on
2026-09-26 with curl 8.21.0 (mingw, Schannel); the commands are in BL-284's Notes.

| Case | Printed |
| --- | --- |
| `-e http://ref.example/x` / no `-e` | `referer` `http://ref.example/x` / nothing |
| `-o out.bin` / `-O` on `wo.txt` / standard output | `filename_effective` `out.bin` / `wo.txt` / nothing |
| two URLs to a server that closes each connection | `conn_id` and `xfer_id` `0`, then `1` |
| connect refused, exit 7 | `conn_id` `0` |
| URL rejected, exit 3 | `conn_id` `-1` |

## Decision

1. `TransferWriteOutVariables` takes them as init properties, `Referer`, `OutputFileName`,
   `ConnectionId` (default `-1`) and `TransferId` (default `0`), so its constructor and
   every existing caller stay as they are.
2. `Curl.Console` sets `Referer` from the `-e` value, `OutputFileName` from the file the
   body was written to (the `-J` name when it chose one, joined with `--output-dir`), and
   `TransferId` from the URL's index, since each URL is one transfer.
3. `ConnectionId` counts per run from `0` over the transfers that connect. A transfer that
   ends with exit 1 or 3 is rejected before a connection exists and prints `-1`; exit 3 was
   measured, exit 1 is taken to match because curl refuses an unsupported scheme at the same
   stage. No connection is reused between URLs, so each connecting transfer takes the next
   number.

## Consequences

- `-w` prints all four as measured for the cases above.
- Under `-e "…;auto" -L`, curl prints the referer of the last request, the previous URL of
  a redirect; this prints the `-e` text until a follow-up carries the sent referer.
- When connection reuse arrives, `conn_id` must come from the connection, not a counter.

## Alternatives considered

- **Add constructor parameters.** Four more positional arguments on a six-argument
  constructor, and every caller changed for values most of them do not need.
- **Carry them on `TransferReport`.** ADR-0015 keeps command-line facts off the report.
