---
id: AF-0115
title: SendUnlessStoppedAsync's '!sent.IsCompleted && StopsSending' can become '||' with no test failing
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:SendUnlessStoppedAsync-and:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115:&&
task: BL-1865
tasks: BL-1865
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

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived HttpContinueWaitConnection.cs:115 && [SendUnlessStoppedAsync] '!sent.IsCompleted && StopsSending' -> '||'. It survived in the seed-0 sample as well.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site reproduction: 'survived  Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:115 &&' ('if (!sent.IsCompleted && StopsSending)' -> '||', member SendUnlessStoppedAsync, outcome survived). Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-09 | 2026-10-09_1435.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command with -ExcludeBaselineFailures: HttpContinueWaitConnection.cs:115 'if (!sent.IsCompleted && StopsSending)' -> '||', outcome survived. The baseline left out the 49 tests that already fail; no remaining test killed the mutant. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-10 | 2026-10-10_0123.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: outcome survived at resolved line 115, 'if (!sent.IsCompleted && StopsSending)' mutated to 'if (!sent.IsCompleted || StopsSending)', every test passed (21 baseline-failing auth-using tests left out). Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
