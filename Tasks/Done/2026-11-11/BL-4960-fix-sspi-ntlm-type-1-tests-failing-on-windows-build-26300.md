---
id: BL-4960
title: Fix SSPI NTLM Type 1 tests failing on Windows build 26300
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCompositionNtlmTests.cs, Curl.Console.UnitTests/CurlCompositionSmtpNtlmTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4960 — Fix SSPI NTLM Type 1 tests failing on Windows build 26300

## Goal

`CurlCompositionNtlmTests.RunAsync_WindowsProductionRoute_SendsSspisType1AndType3AsCurl8210Measured` and `CurlCompositionSmtpNtlmTests.RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured` pass on the lane machine (Windows 11 build 26300) as well as on CI's Windows runner.

## Context

Found by BL-4907's quality run on 2026-11-11 (lane 11). Both tests fail on this machine, and BL-4907 did not change them. The pinned prefix `NTLM TlRMTVNTUAABAAAAB4IIog` (flags 0xA2088207) differs from what the machine's SSPI sends: `NTLM TlRMTVNTUAABAAAAl4II4gAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` (flags 0xE2088297, plus a version block). This build's SSPI negotiates other Type 1 flags. Measure real curl 8.22.0 (Schannel) on this machine with `Record-CurlExchange.ps1` before you change the pin. Then decide whether the tests should assert only the parts of the message that do not depend on the OS build.

## Acceptance criteria

- [x] Both tests pass on Windows build 26300, and each asserts only what curl's own output on that build shows.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Already fixed by BL-4912 (9673d99c8): both tests pin only the Type 1 header. Verified: both pass on build 26300; Curl.Console.UnitTests fast run 3134 passed, 0 failed; build clean.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 490e00b4 a0b49a29.
- 2026-11-11: Doing -> Done. Already fixed by BL-4912; both SSPI NTLM tests verified passing on build 26300
