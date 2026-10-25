---
id: BL-3496
title: Fix AF-0430: CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher checks only that a dispatcher is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3496 — Fix AF-0430: CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher checks only that a dispatcher is not null

## Goal

The defect the audit office reported as AF-0430 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0430 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0430-createdispatcher-wildcardmatchandinterleaveset-bui.md`.

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs:156`

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs:156`

The whole body after setup is Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));. CreateDispatcher does not return null, so the assertion cannot fail; whether WildcardMatch and the Interleave callback reach the handlers is never checked. This is the same pattern AF-0417 fixed in CreateDispatcher_NoTimeout_BuildsTheProductionHandlers.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs -Pattern 'WildcardMatchAndInterleaveSet_BuildsTheDispatcher' -Context 0,10
```

- Expected: An assertion on what the dispatcher does with WildcardMatch and Interleave set (e.g. which handler serves ftp).
- Actual: Line 156: Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null)); is the only assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
