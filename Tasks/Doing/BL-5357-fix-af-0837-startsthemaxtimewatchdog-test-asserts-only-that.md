---
id: BL-5357
title: Fix AF-0837: StartsTheMaxTimeWatchdog test asserts only that OperationStarted is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5357 — Fix AF-0837: StartsTheMaxTimeWatchdog test asserts only that OperationStarted is not null

## Goal

The defect the audit office reported as AF-0837 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0837 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs:173`

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs:173`

The only assertion is Assert.IsNotNull(handler.Contexts.Single().OperationStarted);. Nothing shows a max-time watchdog was started or that it fires: the 1 s timeout never elapses on the SteppingTimeProvider. A perform that started no watchdog but set OperationStarted passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs -SimpleMatch -Pattern 'Assert.IsNotNull(handler.Contexts.Single().OperationStarted);'
```

- Expected: No match: the test advances the clock past the timeout and asserts OperationTimedOut.
- Actual: ManagedEasyTransferTests.ToolProgress.cs:173: Assert.IsNotNull(handler.Contexts.Single().OperationStarted);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
