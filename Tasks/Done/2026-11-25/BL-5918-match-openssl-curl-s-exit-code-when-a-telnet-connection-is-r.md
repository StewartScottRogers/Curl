---
id: BL-5918
title: Match OpenSSL curl's exit code when a TELNET connection is reset on Linux and macOS
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5882]
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetOutbox.cs, Curl.Original.Protocol.Telnet.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Telnet.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-1352-telnet-read-failing-during-option-negotiation-exits-56.md]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5918 — Match OpenSSL curl's exit code when a TELNET connection is reset on Linux and macOS

## Goal

On Linux and macOS a `telnet://` session whose connection is reset ends with the exit code and message OpenSSL curl gives.

## Context

BL-5882 and ADR-1352 pin the Windows behaviour: a reset exits 0 unless it strikes the option negotiation (then 56). The handler applies that on every platform. curl's non-Windows telnet loop (`lib/telnet.c`, `telnet_do`) polls the socket and calls `recv`, so a reset there is expected to fail the `recv` and exit 56 `Recv failure: Connection reset by peer`. Measure it (curl 8.18.0 or later on Linux) before changing `TelnetProtocolHandler.FailedReadResult`.

## Acceptance criteria

- [x] The exit code and stderr of real curl on Linux for a reset of an idle TELNET session, with and without negotiation, are recorded under Notes.
- [x] Unit tests marked with `OSCondition` pin each platform's answer in `Curl.Original.Protocol.Telnet.UnitTests`, and the Windows answers of ADR-1352 are unchanged.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured 2026-11-25: curl 8.18.0 (OpenSSL 3.5.5, Ubuntu in WSL), `curl -sS -v -m 10
telnet://<Windows host>:<port>`, through `Record-CurlExchange.ps1 -ListenAddress 172.26.96.1
-Curl wsl.exe`, standard input at end of file.

| Server | Exit | stderr (besides `-v` negotiation lines) |
|---|---|---|
| reset on accept (`-Reset`) | 56 | `curl: (56) Recv failure: Connection reset by peer` |
| idle, nothing sent, reset after 500 ms | 56 | same |
| `login: `, reset after 500 ms or at once | 56 | same; `login: ` on stdout |
| `IAC DO TERM-TYPE`, reset 500 ms after the replies | 56 | same |
| the same plus `login: ` | 56 | same; `login: ` on stdout |
| negotiation (with or without data), reset before any reply goes out | 0 | `-v` only: `* Sending data failed (104)`, then `(32)` |

Each 56 also prints `* Recv failure: Connection reset by peer` before
`* shutting down connection #0`.

- Curl published self-contained for `linux-x64` (`-p:PublishAot=false`) and run against the same
  server gave the same exit codes, stdout and messages in all rows; the only difference is `(32)`
  instead of `(104)` for the first reply in the last row, filed as BL-5939.
- The recorder's reset options really sent a FIN then a RST, so Linux read an ordinary close and
  every case first measured as exit 0. `Record-CurlExchange.ps1` now closes the socket itself
  (`$client.Client.Close(0)`) at its three reset sites; added to `touches` (no task in Doing held it).
- curl `master` `lib/telnet.c` keeps `ECONNRESET` as the receive error (its macOS comment only adds
  a debug log), so macOS takes the Linux answer; macOS itself is not measured.
- Design: every failed read off Windows is exit 56 (ADR-1382, amends ADR-1352). The handler's
  internal `RunsOnWindows` lets tests cover both rules on any platform, so coverage does not depend
  on the CI runner. Tests that pinned exit 0 through the public handler are now Windows-only, each
  with an off-Windows counterpart.
- Checks on Windows: `dotnet build` 0 warnings; fast tests pass (the off-Windows tests are skipped
  here); `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Telnet.UnitLibrary` 0 failing
  members. `dotnet format --verify-no-changes` reports IMPORTS ordering in 12 telnet test files
  that this task did not reorder; the library is clean.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 0dac2e19 2625c88a 3d836d5e.
- 2026-11-25: Doing -> Done. A reset TELNET session exits 56 Recv failure on Linux and macOS, as curl does; Windows answers unchanged
