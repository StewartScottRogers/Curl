# ADR-0202 — `--trace-ids` marks each transfer's lines through a per-transfer view

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-648.

## Context

curl 8.21.0 puts `[<xfer>-<conn>] ` after any `--trace-time` stamp on every `-v`, `--trace` and
`--trace-ascii` line that starts an event, and `[<xfer>-x] ` before the transfer has a
connection (`[0-x] * Added a.test:1:127.0.0.1 to DNS cache`, `[0-x] * URL rejected: ...`). The
second `v` of `-vv` turns it on too, and a `-v` that is the first option of its argument turns it
off, as both do `--trace-time` (measured 2026-09-29, BL-648 Notes).

The run has one `VerboseTransferEventWriter` or `TraceTransferEventWriter`, shared by every
transfer, and under `-Z` transfers report to it at the same time. Our `%{conn_id}` was taken
only when a transfer's `-w` output was written.

## Decision

1. `Curl.Output`'s `TraceIdsPrefix` holds the IDs of the transfer whose event is being written;
   both writers take one (or none) and write its text after the stamp.
2. Each transfer reports through a `TraceIdsTransferEvents` view that, holding a lock on the
   prefix, sets it to its own IDs and forwards the event, so parallel transfers never write each
   other's IDs. `TransferEventOutput.EventsFor` returns the plain writer when `--trace-ids` is off.
3. A transfer takes its `%{conn_id}` at its first event through its connecting view (the
   `--resolve` entries and `-b` files load through a view with no connection, `[n-x]`); its
   `-w` output prints the number it took, so `%{conn_id}` and the marker always agree.

## Consequences

- The `-v` and trace bytes are unchanged without `--trace-ids` or `-vv`.
- `-vv` now writes IDs, as curl 8.21.0 does; its component trace lines (`[SETUP] added`) are
  `--trace-config`'s work (BL-649).
- A transfer whose URL is rejected before connecting writes no `-v` line of its own today, so the
  `[n-x]` form shows only on the lines loaded before a connection.
