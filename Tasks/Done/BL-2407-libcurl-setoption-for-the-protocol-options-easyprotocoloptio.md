---
id: BL-2407
title: Libcurl: SetOption for the protocol options (EasyProtocolOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Protocols, Libcurl.UnitTests/Options/Protocols]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2407 — Libcurl: SetOption for the protocol options (EasyProtocolOptions)

## Goal

Every `CURLOPT_*` of `EasyProtocolOptions` (FTP, SSH, SMTP, TFTP, TELNET, RTSP and `WS_OPTIONS`) is settable and in the options table.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option, one nested record per protocol as the seam has it.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Rows in `Libcurl/Options/Protocols`: `EasyOptionSetters.Protocols.cs` implements `AddProtocolSetters` over `EasyProtocolOptionSetters.AddTo`, one named method per option (36 rows, plus the `FTPAPPEND` and `MAIL_RCPT_ALLLOWFAILS` aliases through the table), the ADR-0565 pattern.
- Value checks follow curl 8.22.0's `setopt.c` and are recorded in ADR-0571 (Decided by Claude under Stewart's delegation).
- Tests: one class per nested record group in `Libcurl.UnitTests/Options/Protocols` (FTP, SSH, SMTP/TFTP/TELNET, RTSP/WebSocket) over a shared `ProtocolOptionAssert`; Libcurl.UnitTests 249 passing.
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. Full fast run green. `Libcurl/CLAUDE.md` stays true (it already describes the per-group parts), so it is unchanged; it is outside this task's `touches` anyway.
- Conformance ratchet untouched (no CLI change); `--ai-help` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl SetOption sets every EasyProtocolOptions option (FTP, SSH, SMTP, TFTP, TELNET, RTSP, WS_OPTIONS) with setopt.c's checks
