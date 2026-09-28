# ADR-0116 — The Console holds the -v lines from "transfer done" until the meter ends

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-411.
Builds on ADR-0099 and ADR-0111.

## Context

Under `-v` without `-s`, curl 8.21.0 writes the progress meter's end - the done status lines
and the line feed after them - before the connection-end `-v` line. Measured 2026-09-27 with
`Record-CurlExchange.ps1`: over a six-byte `200`, `{ [6 bytes data]`, three `\r100 ...` status
lines and a line feed, then `* Connection #0 to host 127.0.0.1:18421 left intact`; over the
same bytes with `Content-Length: 10` (exit 18), `* end of response with 4 bytes missing`, the
line feed alone, `* closing connection #0`, then `curl: (18) ...`.

ADR-0111 gives `ITransferProgress.ReportTransferDone()`, which the HTTP handler calls before
its connection-end line, whether the exchange succeeded or failed. What the meter draws at the
end depends on that outcome - three done draws after a success, one update after a failure
(BL-131, BL-130) - and the outcome reaches `Curl.Console` only when the handler returns, after
the connection-end line is already written.

## Decision

- `CurlCommandRunner` writes the run's `-v` and trace output through a `HoldableStream` over
  standard error. When `TransferProgressRecorder` hears `ReportTransferDone` while it writes the
  meter live and the transfer has started, it holds that stream: every `-v` byte written after
  it is kept.
- Once the handler returns, `WriteProgressAsync` draws the meter's end for the actual result, as
  before, writes it, and then releases the held bytes, before any failure line.
- Under `-L`, the next hop's "transfer started" releases the held bytes before the redirect
  hop's done draws, which keeps the redirect meter's bytes as they were (BL-277).

## Consequences

- The measured bytes hold for success and for failure alike, with no knowledge in the Console
  of any `-v` line's wording and no change to the handler contract.
- A handler that never reports done, or a run with the meter hidden (`-s`, `--no-progress-meter`,
  `-#`, a body on a terminal), is written exactly as before.
- Anything else the runner writes to standard error between "done" and the meter's end would
  jump ahead of the held lines; today nothing does.

## Alternatives considered

- **Draw the done lines at `ReportTransferDone` as if the transfer succeeded.** A transfer that
  fails after the handler reports done (exit 18, 28, 56) would get three done draws instead of
  curl's one update.
- **Add the outcome to `ReportTransferDone`.** A contract change in
  `Curl.Protocol.Abstractions.UnitLibrary` and every handler for what the Console can do alone.
