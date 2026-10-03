# ADR-0368 — `--trace-config smtp` writes curl's SMTP state machine lines

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1163, split from BL-1104 (ADR-0318), following BL-1162's FTP pattern. curl 8.21.0
(Schannel) writes `* [SMTP] ...` lines under `-v --trace-config smtp` (and `protocol`, `all`,
so `-vv`). Measured with `Record-CurlExchange.ps1 -Smtp`: a one-recipient send, a send after
`AUTH CRAM-MD5`, a refused `RCPT`, a refused `DATA`, a body without a final CRLF and a `VRFY`.
`HELO`, `STARTTLS`, a connect-phase failure and a refused message (after the end-of-data mark)
were not measured.

## Decision

1. `SmtpStateTrace` writes the lines through `ITransferEvents.ReportInfo`; the handler's
   `TracesStateMachine` turns it on, and `CurlComposition.TracesSmtp` sets it from the trace
   components. The control channel carries it so every step writes at its own place.
2. A state change is written only when the state changes, as curl's does: a second `RCPT` or
   command writes a doing call but no state change. `AUTH` is entered once its command is sent
   and stays through the challenges.
3. Under the trace the message body goes out a read at a time and the end-of-data mark on its
   own, each its own data event (`} [18 bytes data]`, `} [3 bytes data]`), as measured; without
   it the last piece and the mark stay one send, as before. Each `cr_eob_read` length is the
   bytes read from the upload, before any dot is doubled.
4. Unmeasured, from curl's state names: `HELO` and `STARTTLS` are their own states and a TLS
   upgrade enters `UPGRADETLS`. A failure before the DO phase writes no doing or done line; a
   refused message, in `POSTDATA`, writes `smtp_done(status=0, premature=0) -> 8`.

## Consequences

Curl's stderr matched real curl's byte for byte for all six measured runs. Without
`--trace-config smtp` nothing changes, including the single data event a stdin upload makes
where curl writes `} [18 bytes data]` for 21 bytes sent; that pre-existing difference is left to
its own task.

## Alternatives considered

- A flag on every SMTP class instead of the channel carrying the trace: more constructor
  churn for the same lines.
- Sending body and mark separately always: changes the measured `-v` data line for `-T file`
  (BL-546), which stays one event.
