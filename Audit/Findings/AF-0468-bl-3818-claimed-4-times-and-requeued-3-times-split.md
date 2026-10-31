---
id: AF-0468
title: BL-3818 claimed 4 times and requeued 3 times, splitting off BL-4026, BL-4044 and BL-4056 one run at a time
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-3818:redone-work
reproduction: none
task: BL-4186
tasks: BL-4186
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0468 - BL-3818 claimed 4 times and requeued 3 times, splitting off BL-4026, BL-4044 and BL-4056 one run at a time

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261029-014637-L2.log:41`: BL-3818 claimed 4 times and requeued 3 times, splitting off BL-4026, BL-4044 and BL-4056 one run at a time.

## Evidence

Location: `logs/DarkFactory-20261029-014637-L2.log:41`

The task Log shows 4 claims (2026-10-28 to 10-29); the tool reports claims=3, outcome requeued, cost $0.89. Requeues: 19:52:56 'Needs BL-4026 (ConnectTarget SNI host ...)' (DarkFactory-20261028-172341-L8.log:67); 22:03:05 'Code for the ldaps second connection landed; real-curl measurement and NTLM flags left to BL-4044' (DarkFactory-20261028-201954-L4.log:41); 02:44:44 'the --cacert variant is BL-4056's work' (DarkFactory-20261029-014637-L2.log:41). Done on claim 4. Each run found the next missing piece, so scoping happened across claims instead of before the first.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3818 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3818: 3 REQUEUE lines; 4 claims in its task Log.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
