---
id: BL-1959
title: Re-fix AF-0115: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1959 — Re-fix AF-0115: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing

## Goal

The defect the audit office reported as AF-0115 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0115 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0115-sendunlessstoppedasync-s-sent-iscompleted-stopssen.md`.

Re-fix: the earlier task(s) BL-1865 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-10-10 | 2026-10-10_0123.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: outcome survived at resolved line 115, 'if (!sent.IsCompleted && StopsSending)' mutated to 'if (!sent.IsCompleted || StopsSending)', every test passed (21 baseline-failing auth-using tests left out). Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115`

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115`

Mutant 'if (!sent.IsCompleted && StopsSending)' -> 'if (!sent.IsCompleted || StopsSending)' survived. With the mutant, a status line that arrives while a body piece is still being written cancels the write even when StopsSending is false (a status below 300, which curl keeps sending through), and the method returns false. The upload is then cut short mid-body and the request bytes differ. The tests' scripted connections finish every write synchronously, so the branch where the status line wins the race and does not stop the upload is never run.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115:&& -Member SendUnlessStoppedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: BL-1865's test `SendUnlessStoppedAsync_Below300ArrivesDuringTheWrite_LetsItFinish`
  raced the mutant. The mutant calls `sending.CancelAsync()`, which sets
  `IsCancellationRequested` at once but carries the cancellation to the write on a thread-pool
  thread; the test's `FinishWrites()` often finished the write first, so `WriteCancelled`
  stayed false and the row passed. With the mutant applied by hand, 1 of the 3 rows failed in
  this lane; in the audit's run, none did.
- Fix (tests only, no production change): `Fakes/HeldWriteConnection` keeps the latest
  write's token and exposes `WriteCancellationRequested`. The test reads it right after
  `DeliverResponse()` and before `FinishWrites()`, and asserts it false. With the mutant
  applied by hand, all 3 rows now fail; with the original line, all pass.
- `touches` widened to `Curl.Protocol.Http.UnitTests` (the fake and test live there); no task
  in Doing on origin/work/dark-factory names it (BL-1961: Networking, BL-1979: Tftp).
- The audit guard refuses lanes `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was
  applied with the Edit tool and the class's tests run with `dotnet test --filter`; the
  re-audit's own run of the reproduction decides the finding.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. SendUnlessStoppedAsync's && -> || mutant now fails every row of the mid-write below-300 test (cancellation request checked before the write finishes)
