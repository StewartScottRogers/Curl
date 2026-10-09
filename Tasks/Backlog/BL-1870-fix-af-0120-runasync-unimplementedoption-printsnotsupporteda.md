---
id: BL-1870
title: Fix AF-0120: RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1870 — Fix AF-0120: RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs

## Goal

The defect the audit office reported as AF-0120 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0120 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0120-runasync-unimplementedoption-printsnotsupportedand.md`.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:50`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:50`

Line 48-51: 'if (name is null) { Assert.Inconclusive("Every curl 8.21.0 option has a row: nothing is unimplemented."); }'. FirstUnimplementedName is null on this tree, so both data rows end Inconclusive on every run and the not-supported path (exit 2, the 'installed libcurl version does not support this' text, no transfer) is never tested here.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Console.UnitTests -c Release -nologo --filter "FullyQualifiedName~RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer"
```

- Expected: Both rows pass.
- Actual: Skipped RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer (True) and (False); Total tests: 2, Skipped: 2.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
