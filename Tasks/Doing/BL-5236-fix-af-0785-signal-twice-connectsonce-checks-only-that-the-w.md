---
id: BL-5236
title: Fix AF-0785: Signal_Twice_ConnectsOnce checks only that the wakeup is readable, not that it connected once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5236 — Fix AF-0785: Signal_Twice_ConnectsOnce checks only that the wakeup is readable, not that it connected once

## Goal

The defect the audit office reported as AF-0785 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0785 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:98`

Location: `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:98`

Step 1 sample. Body: wakeup.Signal(); wakeup.Signal(); Assert.IsTrue(IsReadable(wakeup.Handle, ...)). LoopbackLookupWakeup.Signal guards with 'caller is not null' so a second call does nothing. A second connect would leave the handle just as readable, so the 'Once' in the name is not tested.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs -Pattern 'Signal_Twice_ConnectsOnce' -Context 0,8
```

- Expected: An assertion that exactly one connection reached the listener.
- Actual: Only Assert.IsTrue(IsReadable(wakeup.Handle, TimeSpan.FromSeconds(5))).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
