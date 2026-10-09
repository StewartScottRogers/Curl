---
id: AF-0115
title: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:SendUnlessStoppedAsync-and:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115:&&
task: none
tasks:
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0115 - SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115`: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115`

Mutant 'if (!sent.IsCompleted && StopsSending)' -> 'if (!sent.IsCompleted || StopsSending)' survived. With the mutant, a status line that arrives while a body piece is still being written cancels the write even when StopsSending is false (a status below 300, which curl keeps sending through), and the method returns false. The upload is then cut short mid-body and the request bytes differ. The tests' scripted connections finish every write synchronously, so the branch where the status line wins the race and does not stop the upload is never run.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115:&& -Member SendUnlessStoppedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-08: filed proposed.
