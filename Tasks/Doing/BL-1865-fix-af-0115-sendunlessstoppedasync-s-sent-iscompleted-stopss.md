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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
