---
id: BL-4704
title: Fix AF-0556: Lib566 RunAsync_TransferFails_WritesNothing never checks that nothing was written
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4704 — Fix AF-0556: Lib566 RunAsync_TransferFails_WritesNothing never checks that nothing was written

## Goal

The defect the audit office reported as AF-0556 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0556 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0556-lib566-runasync-transferfails-writesnothing-never.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:404`

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:404`

The body's only assertion is 'Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);'. Neither run.OutputText nor any file Lib566 writes its Content-Length line to is checked, so a port that wrote output after the failure passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs -Pattern 'RunAsync_TransferFails_WritesNothing' -Context 0,7
```

- Expected: An assertion that the output is empty.
- Actual: Only Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now also asserts run.OutputText is empty and the Content-Length file is never created (path variant). Fast Lib566 tests pass. Test-only change, no production code.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
