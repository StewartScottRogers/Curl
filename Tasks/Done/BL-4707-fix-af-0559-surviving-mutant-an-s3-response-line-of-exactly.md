---
id: BL-4707
title: Fix AF-0559: Surviving mutant: an S3 response line of exactly MaxLineLength bytes can be refused as too long without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary, Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpClientConnectionTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4707 — Fix AF-0559: Surviving mutant: an S3 response line of exactly MaxLineLength bytes can be refused as too long without a test failing

## Goal

The defect the audit office reported as AF-0559 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0559 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0559-surviving-mutant-an-s3-response-line-of-exactly-ma.md`.

Location: `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29`

Location: `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29`

Mutant 'line.WrittenCount + take > MaxLineLength' -> '>=' survived. The only limit test (SendAsync_LineLongerThanTheLimit_FailsLineTooLong) sends 'X-Long: ' plus MaxLineLength bytes, well past the limit; nothing pins a line of exactly the limit, which the mutant refuses with S3HttpFailure.LineTooLong.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs:29:> -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-03: Test-only fix, so no production change and no quality run is needed. Added `SendAsync_LineOfExactlyTheLimit_ReadsTheLine`, a header line of exactly `MaxLineLength` bytes before the LF (the CR counts), which must be read, and `SendAsync_LineOneByteOverTheLimit_FailsLineTooLong`, one byte longer. Widened `touches` to the test file (`Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpClientConnectionTests.cs`) because the test belongs there; no task in Doing on `origin/work/dark-factory` names it. A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`, so I applied the mutant (`>` to `>=` at S3HttpConnectionReader.cs:29) by hand: the new test failed, which means the mutant is killed. Then I reverted it. The re-audit confirms the fix.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. S3 response line of exactly MaxLineLength bytes is pinned by a test that kills AF-0559's mutant
