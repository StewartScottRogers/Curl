---
id: BL-5939
title: Report errno 104 for the first TELNET reply a reset connection refuses on Linux, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitTests, Curl.Networking.UnitLibrary/StreamConnection.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/StreamConnectionTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5939 — Report errno 104 for the first TELNET reply a reset connection refuses on Linux, as curl does

## Goal

On Linux, `-v` output for a TELNET server that resets before any negotiation reply goes out reports `Sending data failed (104)` for the first reply, as curl does.

## Context

Found by BL-5918 (ADR-1382), 2026-11-25, in WSL Ubuntu: curl 8.18.0 printed
`Sending data failed (104)` then `(32)` for the rest; Curl published for `linux-x64` printed
`(32)` for all six. Measure with `Record-CurlExchange.ps1 -ResetAfterResponse -Response '\xFF\xFD\x18'
-ListenAddress <WSL host address> -Curl wsl.exe`. First find where the socket's pending error is
taken before the first send (`TelnetProtocolHandler.TrySendRepliesAsync`, or the connection
under it, which may need another file in `touches`).

## Acceptance criteria

- [x] The cause of the first `(32)` is recorded under Notes with how it was measured.
- [x] Curl on Linux prints the same `Sending data failed` numbers as curl for that exchange, pinned by a unit test in the fixed library's `.UnitTests` project, or the Notes show the .NET base class library cannot see the pending error and an ADR records the difference.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `StreamConnection.WriteAsync` (`Curl.Networking.UnitLibrary`) ran the ADR-0708/ADR-0878
  check before every write on every platform, though only the Windows aborted-to-reset rewrite
  reads its answer. After a first read that check peeks one byte from a readable socket; on Linux
  the peek of a reset socket fails with the pending `ECONNRESET` and clears it, so the first
  reply's send got `EPIPE`. Nothing in the TELNET handler was at fault.
- Fix: the check runs only under `ReportsAbortedReadAsReset` (Windows). ADR-1386.
- `touches` widened to the three `Curl.Networking` files: no other task in Doing on
  `origin/work/dark-factory` named any of them.
- Measured 2026-11-25 in WSL Ubuntu: Curl published for `linux-x64` (self-contained,
  `-p:PublishAot=false`) and curl 8.18.0 against `Record-CurlExchange.ps1 -ResetAfterResponse
  -Response '\xFF\xFD\x18\xFF\xFD\x1F\xFF\xFB\x01' -ListenAddress 172.26.96.1 -Curl wsl.exe`,
  `-v -s -S telnet://...`. After the fix Curl's `-v` lines after the connection lines matched
  curl's (`(104)`, then `(32)`, `Send failure: Broken pipe` included), exit 0. curl takes that
  outcome or exit 56 by timing (2 of 4 runs each). The "before" `(32)` for every reply is
  BL-5918's measurement; the match after the one-condition change confirms the cause.
- Pinned by `StreamConnectionTests.WriteAsync_ChecksTheSocketBeforeWritingOnlyWhenAbortsAreReportedAsResets`.
- Checks: build clean; fast tests green; `Curl.Networking.IntegrationTests` 18 passed, 2 skipped;
  `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line and branch, 0 failing.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: e86cae23 681bf664 3d836d5e.
- 2026-11-25: Doing -> Done. On Linux and macOS a write no longer peeks the socket first, so the first TELNET reply a reset refuses reports 104 as curl does (ADR-1386).
