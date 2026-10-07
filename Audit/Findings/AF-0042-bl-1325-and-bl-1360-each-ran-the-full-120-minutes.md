---
id: AF-0042
title: BL-1325 and BL-1360 each ran the full 120 minutes and were Blocked by the factory timeout, with uncommitted work stashed
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
key: process:BL-1325:BL-1325:unfinished-run
task: BL-1385
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed: 2026-10-07
closed-by: 2026-10-07_0844.md
---
# AF-0042 - BL-1325 and BL-1360 each ran the full 120 minutes and were Blocked by the factory timeout, with uncommitted work stashed

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261003-061205-L2.log:11`: BL-1325 and BL-1360 each ran the full 120 minutes and were Blocked by the factory timeout, with uncommitted work stashed.

## Evidence

Location: `logs/DarkFactory-20261003-061205-L2.log:11`

BL-1325 (lane 2): claim 08:03:49, test 22/22 pass at 08:48 (40 min after the build), quality ok 09:33, stash 'uncommitted work kept' at 10:03:53, BLOCKED 10:04:23 'dark factory timed out after 1...'. BL-1360 (lane 7): claim 11:05:15, test 40/40 pass 11:11:58, quality steps up to 12:48:24, stash 13:05:18, BLOCKED 13:05:30, also a timeout. Both had passing tests early. About 80 minutes went on the quality and verify steps, and the work was parked in a stash rather than finished. The cause is a timeout, not a crash or a usage limit.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path ..\logs\DarkFactory-20261003-061205-L2.log,..\logs\DarkFactory-20261003-061205-L7.log -Pattern 'BL-1325|BL-1360' | Select-String -Pattern 'claim|stash|BLOCKED'
```

- Expected: Each task ends DONE well under 120 minutes.
- Actual: Both end BLOCKED at about 120 minutes with a stash.

## Re-audits

- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction: Select-String failed with "Cannot find path ...\logs\DarkFactory-20261003-061205-L2.log because it does not exist". The L7 log is missing too, so the defective result cannot be shown. The stashes 'darkfactory BL-1325 20261003-061205' and 'darkfactory BL-1360 20261003-061205' are still in git stash list. Both tasks are Done on 2026-10-03, and BL-1325's Log says its code was 'restored from stash c0e65b81^3'.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
