---
id: AF-0473
title: BL-4012 requeued twice (claimed with template-placeholder criteria, then the run budget ran out with stashed work) and then Deferred
auditor: process
severity: Medium
status: accepted
reason: 
key: process:logs:BL-4012:redone-work
reproduction: none
task: BL-4190
tasks: BL-4190
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0473 - BL-4012 requeued twice (claimed with template-placeholder criteria, then the run budget ran out with stashed work) and then Deferred

## Summary

Medium finding from the process auditor at `logs/DarkFactory-20261028-201954-L6.log:19`: BL-4012 requeued twice (claimed with template-placeholder criteria, then the run budget ran out with stashed work) and then Deferred.

## Evidence

Location: `logs/DarkFactory-20261028-201954-L6.log:19`

Requeue 1 at 2026-10-28 19:06:36: 'Waits on BL-4018 ...; Goal and criteria are still the template placeholders and must be written in Backlog' (DarkFactory-20261028-172341-L11.log:65). A task with no real criteria was claimable. Requeue 2 at 21:13:58: 'Run budget ran out; 3 quality-gate failures left in the stashed work' (DarkFactory-20261028-201954-L6.log:19). The task was then Deferred and split into BL-4057 and BL-4058. The tool reports claims=2, 73.4 min, cost $2.72.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-4012 (claim|REQUEUE)'
```

- Expected: Fewer than 2 requeues; no claim of a task whose criteria are template placeholders.
- Actual: BL-4012: 2 REQUEUE lines, outcome requeued, then Deferred.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | The run budget again ran out twice with the work stashed. BL-4170 was requeued at 09:24:52 ('Run budget spent after the tokenizer (left uncommitted for the shift's stash)') and at 14:29:53 ('Run budget spent after the parser and descriptors (uncommitted for the shift's stash)'), then Blocked on the cost cap. No template-placeholder criteria were seen in this window.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
