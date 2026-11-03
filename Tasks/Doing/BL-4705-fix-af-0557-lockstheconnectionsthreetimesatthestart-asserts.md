---
id: BL-4705
title: Fix AF-0557: LocksTheConnectionsThreeTimesAtTheStart asserts four locks over the whole perform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4705 — Fix AF-0557: LocksTheConnectionsThreeTimesAtTheStart asserts four locks over the whole perform

## Goal

The defect the audit office reported as AF-0557 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0557 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0557-lockstheconnectionsthreetimesatthestart-asserts-fo.md`.

Location: `Curl.Engine.UnitTests/ManagedShareHandleTests.cs:122`

Location: `Curl.Engine.UnitTests/ManagedShareHandleTests.cs:122`

The name promises three Connect locks at the start; the test counts every Connect lock of the whole perform and asserts 'Assert.AreEqual(4, locks);'. Name and assertion disagree, so a change to the start-of-perform lock count is indistinguishable from one at the end.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedShareHandleTests.cs -Pattern 'LocksTheConnectionsThreeTimesAtTheStart|Assert.AreEqual\(4, locks\)'
```

- Expected: A test that counts the start's locks and asserts 3 (or a name that says four).
- Actual: Name says ThreeTimesAtTheStart; ManagedShareHandleTests.cs:122: Assert.AreEqual(4, locks);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
