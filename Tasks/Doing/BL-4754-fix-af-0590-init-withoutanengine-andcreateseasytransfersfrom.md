---
id: BL-4754
title: Fix AF-0590: Init_WithoutAnEngine_..._AndCreatesEasyTransfersFromIt checks the transfer only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4754 — Fix AF-0590: Init_WithoutAnEngine_..._AndCreatesEasyTransfersFromIt checks the transfer only for null

## Goal

The defect the audit office reported as AF-0590 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0590 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0590-init-withoutanengine-andcreateseasytransfersfromit.md`.

Location: `Libcurl.UnitTests/Global/CurlGlobalTests.cs:75`

Location: `Libcurl.UnitTests/Global/CurlGlobalTests.cs:75`

Step 0 weak-assertion. The name says the easy transfers come from the managed engine; Assert.IsNotNull(transfer); is the only check of transfer, so one created elsewhere would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Libcurl.UnitTests/Global/CurlGlobalTests.cs -Pattern 'Assert.IsNotNull\(transfer\)'
```

- Expected: No match: the test asserts the transfer is the managed engine's (e.g. its type).
- Actual: CurlGlobalTests.cs:75: Assert.IsNotNull(transfer);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
