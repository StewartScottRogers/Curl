---
id: BL-3449
title: Fix AF-0402: Of_VerboseWithADebugCallback_GivesTheEvents asserts only that a result is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-24
completed:
---
# BL-3449 — Fix AF-0402: Of_VerboseWithADebugCallback_GivesTheEvents asserts only that a result is not null

## Goal

The defect the audit office reported as AF-0402 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0402 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0402-of-verbosewithadebugcallback-givestheevents-assert.md`.

Location: `Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs:34`

Location: `Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs:34`

The whole body is Assert.IsNotNull(DebugCallbackTransferEvents.Of(verbose: true, (_, _) => 0));. The name promises the debug-callback events are given; any non-null ITransferEvents - including one that ignores the callback - passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs -Pattern 'Of_VerboseWithADebugCallback_GivesTheEvents' -Context 0,3
```

- Expected: The test asserts the returned events' type or that a reported event reaches the callback.
- Actual: DebugCallbackTransferEventsTests.cs:34:        Assert.IsNotNull(DebugCallbackTransferEvents.Of(verbose: true, (_, _) => 0));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
