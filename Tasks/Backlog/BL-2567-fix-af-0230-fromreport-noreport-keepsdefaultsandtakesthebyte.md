---
id: BL-2567
title: Fix AF-0230: FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred asserts nothing; its checks only log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2567 — Fix AF-0230: FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred asserts nothing; its checks only log

## Goal

The defect the audit office reported as AF-0230 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0230 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0230-fromreport-noreport-keepsdefaultsandtakesthebytest.md`.

Location: `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:50`

Location: `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:50`

Lines 57-60 are diagnostics.Assert("effective url", RequestUrl, ...), ("download size", 12L, ...), ("total time", ...), ("primary port", 0L, ...). TestDiagnostics.Assert only logs (TestDiagnostics.cs:189-193), so the defaults and the bytes transferred that the name promises are never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/WriteOutTransferInfoTests.cs -Pattern 'Assert\.' | Select-Object LineNumber
```

- Expected: Assert.* lines between 57 and 60.
- Actual: Only lines 70, 71 and 72.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
