---
id: BL-5939
title: Report errno 104 for the first TELNET reply a reset connection refuses on Linux, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-25
completed:
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

- [ ] The cause of the first `(32)` is recorded under Notes with how it was measured.
- [ ] Curl on Linux prints the same `Sending data failed` numbers as curl for that exchange, pinned by a unit test in the fixed library's `.UnitTests` project, or the Notes show the .NET base class library cannot see the pending error and an ADR records the difference.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
