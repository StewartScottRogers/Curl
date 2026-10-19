---
id: BL-2799
title: Fix AF-0272: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/EngineCompositionFtpTraceTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2799 — Fix AF-0272: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP

## Goal

The defect the audit office reported as AF-0272 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0272 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0272-createftpprotocolhandler-s-tracesftp-default-can-b.md`.

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:230`

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:230`

Mutant bool tracesFtp = false -> true survived (sampled run, seed 0). The default is not equivalent: ManagedCurlEngine.cs:232 builds the CURLOPT_CONNECT_ONLY FTP login handler with EngineComposition.CreateFtpProtocolHandler(connector, connector, transports.TlsProvider, transports.DnsResolver), relying on the default. With the mutant that handler sets TracesStateMachine and writes --trace-config ftp lines nobody asked for into verbose/debug output.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:230:false -Member CreateFtpProtocolHandler -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-19 (lane 3): No production change is needed. The mutant survives only because no test calls `CreateFtpProtocolHandler` without `tracesFtp` and then checks `TracesStateMachine`. The fix is one test in `Curl.Engine.UnitTests/EngineCompositionFtpTraceTests.cs` that builds the handler with only the four required arguments, as `ManagedCurlEngine`'s CURLOPT_CONNECT_ONLY path does, and asserts `TracesStateMachine` is false. `touches` was narrowed from `Curl.Engine.UnitLibrary`, which stays unchanged, to that one test file. BL-2804, in Doing, holds the whole of `Curl.Engine.UnitTests`, so this task went back to Backlog until the two no longer overlap.

- 2026-10-19 (lane 1): Added `CreateFtpProtocolHandler_WithoutTracesFtp_DoesNotTraceTheStateMachine` in `EngineCompositionFtpTraceTests`: it builds the handler with the four required arguments, as `ManagedCurlEngine`'s connect-only login path does, and asserts `TracesStateMachine` is false, so the `tracesFtp = true` mutant fails it. `Audit/Tools/Invoke-MutationTest.ps1` was not run here: audit paths are closed to lanes, and a hand-applied mutant would edit `Curl.Engine.UnitLibrary`, outside `touches`. The quality auditor's re-audit runs the reproduction. Curl.Engine.UnitTests fast tests: 2142 passed, 0 failed. No production code changed, so no quality run.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Backlog. touches Curl.Engine.UnitTests/EngineCompositionFtpTraceTests.cs, inside Curl.Engine.UnitTests, which BL-2804 (Doing) holds whole
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. Test pins CreateFtpProtocolHandler's tracesFtp default as false; Curl.Engine fast tests green
