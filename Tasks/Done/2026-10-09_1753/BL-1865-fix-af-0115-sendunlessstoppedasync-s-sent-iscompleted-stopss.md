---
id: BL-1865
title: Fix AF-0115: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1865 — Fix AF-0115: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing

## Goal

The defect the audit office reported as AF-0115 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0115 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0115-sendunlessstoppedasync-s-sent-iscompleted-stopssen.md`.

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

- The fix is a test, not a code change: the production line was right, only the race it
  guards was never run. `SendUnlessStoppedAsync_Below300ArrivesDuringTheWrite_LetsItFinish`
  (100, 200 and 299 rows) delivers the status line while the body write is still pending
  and then finishes the write; it asserts the write was not cancelled, the send returned
  true and the byte reached the connection. The new fake `Fakes/HeldWriteConnection`
  holds reads and writes until the test releases them, and completes its waiters on the
  caller's thread so the send's check has run before the write is finished.
  `PastTheWaitAsync` now takes any `IConnection`.
- The reproduction script lives under `Audit/`, which a lane may not run
  (guard-audit-paths), so the mutant was applied by hand instead: with
  `!sent.IsCompleted || StopsSending` all three new rows fail (28 tests, 3 failed); with
  the original `&&` all 28 pass. The quality auditor's re-audit runs the script itself.
- The library is unchanged, so its coverage and complexity are unchanged; no
  Measure-CodeQuality run was needed. Only `Curl.Protocol.Http.UnitTests` changed, the
  test project of the library in `touches`.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Test now runs the below-300 status arriving mid-write; the || mutant fails it
