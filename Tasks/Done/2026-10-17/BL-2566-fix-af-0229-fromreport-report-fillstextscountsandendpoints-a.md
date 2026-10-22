---
id: BL-2566
title: Fix AF-0229: FromReport_Report_FillsTextsCountsAndEndPoints asserts nothing; its checks are TestDiagnostics.Assert log lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2566 — Fix AF-0229: FromReport_Report_FillsTextsCountsAndEndPoints asserts nothing; its checks are TestDiagnostics.Assert log lines

## Goal

The defect the audit office reported as AF-0229 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0229 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0229-fromreport-report-fillstextscountsandendpoints-ass.md`.

Location: `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:19`

Location: `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:19`

Lines 42-46 are the test's only checks: diagnostics.Assert("effective url", ...), ("method", ...), ("codes", "201 200 1 1 1 18", ...), ("sizes", ...), ("end points", ...). TestDiagnostics.Assert never throws (TestDiagnostics.cs:189-193), so a FromReport that dropped the response code, the proxy CONNECT code, the sizes or the end points would pass. The file's only Assert calls are the three Assert.ThrowsExactly at lines 70-72 in another test. Found by a tree-wide scan for tests whose only checks go through TestDiagnostics.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/WriteOutTransferInfoTests.cs -Pattern 'Assert\.' | Select-Object LineNumber
```

- Expected: Assert.* lines between 42 and 46, in FromReport_Report_FillsTextsCountsAndEndPoints.
- Actual: Only lines 70, 71 and 72 (Assert.ThrowsExactly in FromReport_NullArgument_Throws).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Each check in FromReport_Report_FillsTextsCountsAndEndPoints is now Assert.AreEqual, with the values logged through diagnostics.Act (the shape BL-2565 used). FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred had the same defect and FromReport_NullArgument_Throws checked its parameter names the same way, so both now use Assert.AreEqual too. Reproduction now lists Assert lines 50-54 inside the test. Output tests 663 passed; full build clean, fast tests green.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. WriteOutTransferInfo tests assert with Assert.AreEqual; reproduction gives expected lines
