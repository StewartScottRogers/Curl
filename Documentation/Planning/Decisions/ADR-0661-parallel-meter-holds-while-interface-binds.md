# ADR-0661 — The `-Z` meter draws nothing while a transfer binds `--interface`

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2574 (AF-0237).

## Context

ADR-0155 has `ParallelProgressMeter` draw on the run's events and on a one-second idle tick. curl 8.22.0 binds
`--interface` inside its connect step and, when the name is not an interface or address, resolves it as a host
and waits for the answer (`bindlocal` in `lib/cf-socket.c`), so `curl_multi_perform` blocks and its parallel loop
draws nothing until the step returns. Measured with `curl --parallel --interface - http://127.0.0.1:63411/`
(Schannel build, `Record-CurlExchange.ps1`): curl's standard error starts with
`curl: (45) Failed to connect to 127.0.0.1:63411 after 2986 ms: Failed binding local connection end`, then the
meter's header and its last two lines. Curl's asynchronous connect let the meter draw its header and a row a
second during the 2.7 s bind, and the `curl: (45)` text ran on after the last row.

## Decision

- A `-Z` transfer given `--interface` is marked live holding the loop
  (`ParallelTransferProgress.MarkLive(bindsInterface: true)`, `HoldsTheLoop`). While any transfer not yet ended
  holds it, `ParallelProgressMeter` draws no line but the final one and does not set its idle draw again.
- The hold ends when the handler reports the transfer past connect (`ReportTransferStarted`, which
  `TransferProgressRecorder` now passes on to the transfer's share), which draws when due, or when the transfer
  ends, whose draw comes after its `curl: (45)` line, as in curl.
- The connector does not report the bind apart from the TCP connect, so the hold lasts until the transfer is past
  connect, not just past the bind. With a real interface the bind is immediate in curl too; the difference is that
  a slow TCP connect behind `--interface` draws no idle lines, where curl's would. That is the simplest change that
  matches the measured case without a new event across `Curl.Protocol.Abstractions.UnitLibrary`.

## Consequences

- AF-0237's reproduction prints `curl: (45) ...` as the first line of standard error, as curl does.
- Runs without `--interface` draw exactly as before.
