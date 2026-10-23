---
id: AF-0386
title: BL-3263 claimed 3 times because two tasks shared its ID, so lanes 6 and 2 both made the same differential-normaliser change
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-3263:redone-work
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0386 - BL-3263 claimed 3 times because two tasks shared its ID, so lanes 6 and 2 both made the same differential-normaliser change

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261023-005934-L2.log:64`: BL-3263 claimed 3 times because two tasks shared its ID, so lanes 6 and 2 both made the same differential-normaliser change.

## Evidence

Location: `logs/DarkFactory-20261023-005934-L2.log:64`

The tool reports BL-3263 claims=3. The shift-end review created BL-3263 (ConcatenatedReadStream), and a second task (differential normaliser progress meter) was given the same ID. L6 claimed BL-3263 at 01:34:55 and did the normaliser change (BL-3263-20261023-005934-L6.jsonl result: 'the differential normaliser now reduces the verbose progress meter...'). It hit a rebase conflict at 01:39:03, renumbered its task 'BL-3263 -> BL-3267' at 01:42:45, and pushed at 01:43:42. L2 claimed BL-3263 at 01:38:51, before the renumbering, and made the same normaliser change again (L2 jsonl result: 'DifferentialOutputNormaliser now collapses the verbose progress meter...'). It then spent two resolve runs (01:44:05 and 01:52:44) on rebase conflicts against L6's identical work, and pushed at 02:00:45. That is 22 lane-minutes redone. L7 then claimed the real task at 01:51:58 and finished it at 02:14:05. BL-3267's Log says 'moved by hand: the board script refused over duplicate ID'. Cause: the board gave out a duplicate ID and both lanes claimed it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-061815\logs\DarkFactory-20261023-005934-L*.log -Pattern 'BL-3263 (claim|renum|resolve|DONE)'
```

- Expected: BL-3263 claimed once; every task ID unique on the board.
- Actual: tasks[BL-3263].claims = 3; claims by L6, L2 and L7, and 'renum BL-3263 -> BL-3267' in L6 at 01:42:45.

## Re-audits

## Log

- 2026-10-23: filed proposed.
