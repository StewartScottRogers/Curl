# ADR-0111 — The progress sink hears when the transfer is done, before the connection-end line

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-478.
Amends ADR-0045.

## Context

curl 8.21.0 draws the progress meter's done status lines, and the line feed that ends them,
in `Curl_pgrsDone`, which `multi_done` calls before it writes the connection-end `-v` line
(`* Connection #0 to host ... left intact`, `closing connection`, `shutting down
connection`). Measured 2026-09-27 (BL-411 Context): `{ [6 bytes data]`, three `\r100 ...`
status lines and a line feed, then `* Connection #0 to host 127.0.0.1:18421 left intact`.

`Curl.Console` draws the done lines when the handler returns, and the HTTP handler has
reported the connection end by then, so under `-v` without `-s` the meter lands after that
line. ADR-0045's `ITransferProgress` has no member that says the data is complete.

## Decision

- **`ITransferProgress.ReportTransferDone()`**: the transfer's data is complete and the
  handler is about to report what became of its connection. It has an empty default body,
  so a handler that never calls it and a sink that ignores it need no change.
  `NoTransferProgress` implements it as nothing; `Curl.Core`'s `LowSpeedWatchdog` wrapper
  and `Curl.Protocol.Http`'s `HttpTransferProgress` pass it on.
- **`HttpProtocolHandler` calls it once per run, for the final exchange only**, after the
  connection is disposed and before `ReportConnectionEnd`: not for a 401 or 417 answered
  with a retry, nor for a pooled connection that died before its response and is sent
  again. The final exchange reports it whether it succeeded or failed, as `multi_done`
  calls `Curl_pgrsDone` for both; the consumer decides what to draw.
- **A failed connect does not report it.** The handler reports nothing to the progress sink
  when no connection is made (ADR-0045), and that stays so.
- Each redirect hop `-L` follows is its own handler run, so each reports it once.

## Consequences

- BL-411 can draw the meter's done lines on this signal, before the connection-end line.
- Other handlers report nothing yet; their consumers see no change until each calls it.

## Alternatives considered

- **Have `Curl.Console` hold back the connection-end line until it draws the meter.** It
  would have to recognise one info line among all of them by its text, which ties the
  Console to the handler's wording.
- **An abstract member with no default body.** Every sink and every test fake in the
  solution would have to implement it at once, for a signal only HTTP sends today.
