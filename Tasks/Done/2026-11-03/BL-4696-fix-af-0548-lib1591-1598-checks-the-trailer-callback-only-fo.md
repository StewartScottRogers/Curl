---
id: BL-4696
title: Fix AF-0548: Lib1591/1598 checks the trailer callback only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4696 — Fix AF-0548: Lib1591/1598 checks the trailer callback only for null

## Goal

The defect the audit office reported as AF-0548 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0548 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0548-lib1591-1598-checks-the-trailer-callback-only-for.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs:124`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs:124`

'Assert.IsNotNull(trailer);' is the only check of the callback the name says is sent; a callback that adds no trailers, or the wrong ones, passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs -Pattern 'Assert.IsNotNull\(trailer\)'
```

- Expected: The callback is invoked and its trailer lines are asserted.
- Actual: Lib1589To1648Tests.cs:124: Assert.IsNotNull(trailer);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only change, so done directly rather than through the feature stages and with no Measure-CodeQuality run (no production code changed). Lib1591And1598_SendTheTrailerHeaderAndCallbackAndAnswerTheirCode now invokes the captured trailer callback and asserts it returns Ok and adds exactly the two trailers. Reproduction finds no match; dotnet build clean, fast tests green.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Lib1591/1598 test invokes the trailer callback and asserts its two trailers
