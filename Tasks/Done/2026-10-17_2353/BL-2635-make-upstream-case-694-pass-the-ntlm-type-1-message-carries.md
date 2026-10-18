---
id: BL-2635
title: Make upstream case 694 pass: the NTLM type-1 message carries curl's flags
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2635 — Make upstream case 694 pass: the NTLM type-1 message carries curl's flags

## Goal

Upstream case 694 passes through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test694 (lib694: `CURLAUTH_BASIC|DIGEST|NTLM` as `me:password`): `differs at byte 146 (line 7): expected "Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", got "...AABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw=="` - Curl's type-1 message sets other flags and a version block; curl 8.22.0's carries `0x00088206` and no version.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 1): the project at fault was `Curl.Engine.UnitLibrary`, not Libcurl: the ratchet's libtest engine (`UpstreamConformanceTests.CreateEngine`) built its handlers' security contexts with the plain router, so on Windows NTLM went to SSPI (flags 0xa2088207 plus VERSION), while `RunCurlAsync` passes `usesHandBuiltNtlm: true`. Added to `touches`: `Curl.Engine.UnitLibrary`, `Curl.Engine.UnitTests` and `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` (no task in Doing on `origin/work/dark-factory` names them).
- Fix: `EngineDialing.UsesHandBuiltNtlm` (default false); when set, `ManagedCurlEngine.CreateDispatcher` and `CreateWebSocketAuthenticator` take `EngineComposition.CreateDialingSecurityContextFactory(..., usesHandBuiltNtlm: true)`, as the dialing runner does. `CreateEngine` sets it. No ADR: it applies the existing ADR-0455 harness choice to the libtest engine. Focused test: `ManagedCurlEngineTests.PerformAsync_DialingUsesHandBuiltNtlm_SendsCurlsOwnTypeOneMessage`.
- Measured after the fix on Windows: test694's requests all match, but its stdout still differs at byte 785 (line 23): expected the end, got `CURLINFO_HTTPAUTH_USED did not say NTLM\n` - the second perform on the handle reports no picked scheme. A different cause, filed as BL-2709; 694 stays off the list.
- Also measured: BL-2689's cases 2025, 2029, 2031 and 2032 now pass; 2030 fails at line 13 (a Digest `Authorization` expected, `Accept: */*` got). Left to BL-2689, which owns them.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members. Fast tests all green.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
- 2026-10-17: Doing -> Done. The libtest engine sends curl's own NTLM type-1 message (EngineDialing.UsesHandBuiltNtlm); 694's remaining HTTPAUTH_USED difference filed as BL-2709
