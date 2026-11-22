---
id: BL-5727
title: Fix AF-0958: Lib1977_WritesEachTransfersEffectiveUrl counts lines starting 'effective URL: ' but never checks a URL
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5727 — Fix AF-0958: Lib1977_WritesEachTransfersEffectiveUrl counts lines starting 'effective URL: ' but never checks a URL

## Goal

The defect the audit office reported as AF-0958 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0958 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:198`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:198`

The only check of the output is Assert.AreEqual(3, run.OutputText.Split('\n', ...).Count(line => line.StartsWith("effective URL: "))). The name says each transfer's effective URL is written (Lib1977.cs:76 writes $"effective URL: {effective}\n"), but a wrong or empty URL after the prefix passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs -Pattern 'StartsWith\("effective URL: "'
```

- Expected: No match: the test compares the whole output, URLs included
- Actual: Lib1964To1977Tests.cs:198 counts prefixed lines only

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Lib1977 test now sets a fixed EffectiveUrl on the fake engine and asserts the whole output (three exact lines); the StartsWith count is gone.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-21: Doing -> Done. Lib1977 test asserts the full output with exact effective URLs
