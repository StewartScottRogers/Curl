---
id: BL-4959
title: Fix the Windows SSPI NTLM tests failing on the lane host
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCompositionNtlmTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4959 — Fix the Windows SSPI NTLM tests failing on the lane host

## Goal

`RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured` and
`RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured` pass on the lane host,
or depend only on what every Windows host gives them.

## Context

Found by BL-4908 on lane 5 (Windows 11 Pro 10.0.26300) on 2026-11-11: both tests in
`Curl.Console.UnitTests/CurlCompositionNtlmTests.cs` fail in the fast run, and so
`Measure-CodeQuality.ps1` stops ("dotnet test failed") for every library that `Curl.Console.UnitTests`
covers. They call the host's real SSPI (`--ntlm -u u:p`, no output option), so the cause is the host's
NTLM behaviour (this Windows build may change or restrict NTLM), not a code change. BL-4908's change
does not touch NTLM. Start by printing the tokens the host's SSPI returns and comparing them with the
pinned `TlRMTVNTUAABAAAAB4IIog` / `TlRMTVNTUAADAAAAGAAYA` prefixes.

The same host has no .NET SDK 10.0.401 installed (only 10.0.100-rc.1 and 11.0.100), so `global.json`
does not resolve there; BL-4908 ran its tests with a temporary local `global.json` naming 11.0.100.

## Acceptance criteria

- [x] Both tests pass on the lane host, or are rewritten to pin only what every supported Windows build's SSPI sends.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Already fixed by 9673d99c8 (BL-4912), which pins only the Type 1 header prefix and Type 3 prefix; both NTLM tests (and the whole CurlCompositionNtlmTests class) pass on this host: 8 passed, 2 skipped. dotnet build clean. No code change here.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: b258f801 3d836d5e.
- 2026-11-11: Doing -> Done. Already fixed by BL-4912 (9673d99c8); NTLM tests pass on the lane host
