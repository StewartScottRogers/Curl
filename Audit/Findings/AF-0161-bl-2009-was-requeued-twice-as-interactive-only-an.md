---
id: AF-0161
title: BL-2009 was requeued twice as interactive only; an interactive commit removed its lane: no between the two lane claims
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-2009:BL-2009:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0161 - BL-2009 was requeued twice as interactive only; an interactive commit removed its lane: no between the two lane claims

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261010-213213-L1.log:73`: BL-2009 was requeued twice as interactive only; an interactive commit removed its lane: no between the two lane claims.

## Evidence

Location: `logs/DarkFactory-20261010-213213-L1.log:73`

Measure-FactoryProcess row: BL-2009 claims 2, costUsd 2.4277, outcome requeued. Claim 1 at 18:49:00 (DarkFactory-20261010-181711-L1.log:87), 21 turns: 'REQUEUE ... Interactive only: the gap harness and upstream cache for test1642/1643/3036 are refused to lanes ... added lane: no' (line 96). Commit 776f30497 adds '+lane: no'. The interactive commit 4c54032c0 (21:48, 'hand BL-2031 and BL-2009's product-code remainder to lanes') removed it ('-lane: no'). Lane 1 claimed it again at 21:57:24 (DarkFactory-20261010-213213-L1.log:64) and, after 28 turns, requeued it at 22:01:16 for the same reason: 'Interactive only: test3036's retry difference ... appears only in the gap office's cross-check, which lanes may not read' (line 73), re-adding lane: no (84ba607eb). Cause: the hand-back to lanes did not resolve the reason the first lane gave. This also ended the shift with 'nothing ready'.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-*-L*.log -Pattern 'BL-2009 (claim|REQUEUE)'
```

- Expected: At most one lane claim of BL-2009 ends in an 'Interactive only' requeue.
- Actual: Two claims (18:49:00, 21:57:24) and two 'REQUEUE ... Interactive only' lines (18:51:35, 22:01:16).

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
