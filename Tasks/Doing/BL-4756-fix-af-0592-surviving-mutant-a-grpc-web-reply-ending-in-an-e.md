---
id: BL-4756
title: Fix AF-0592: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.GrpcWeb.UnitLibrary, Curl.Extension.GrpcWeb.UnitTests/GrpcWebFramingTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4756 — Fix AF-0592: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing

## Goal

The defect the audit office reported as AF-0592 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0592 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0592-surviving-mutant-a-grpc-web-reply-ending-in-an-emp.md`.

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67`

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67`

Drawn library. rest.Length < PrefixLength -> rest.Length <= PrefixLength survived: a final 5-byte frame with length 0 (an empty message or an empty trailers frame) then reads its length as long.MaxValue and throws 'the reply ends inside a frame', failing a valid reply with a different exit code. No test parses a body that ends with a zero-length frame.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67:< -Member PayloadAt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is test-only. `GrpcWebFraming.PayloadAt` was already right, but no test parsed a body that ends in a zero-length frame. Added `Parse_ABodyEndingInAnEmptyDataFrame_GivesTheEmptyMessage_MessageFraming` and `Parse_ABodyEndingInAnEmptyTrailersFrame_GivesNoTrailerFields_MessageFraming` to `Curl.Extension.GrpcWeb.UnitTests/GrpcWebFramingTests.cs`. That file is now in touches, and no other task in Doing names it.
- Lanes may not run the finding's mutation tool, so the mutant was applied by hand (`<` -> `<=` at GrpcWebFraming.cs:67). Both new tests failed against it, so it is killed. The edit was then reverted. The quality auditor's re-audit runs the finding's own reproduction.
- No production code changed, so there was no Measure-CodeQuality.ps1 run. `dotnet build` is clean and the fast tests are green on Windows. The tests use only byte arrays, so they behave the same on every platform.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
