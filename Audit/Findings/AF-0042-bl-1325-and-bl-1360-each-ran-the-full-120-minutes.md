---
id: AF-0042
title: BL-1325 and BL-1360 each ran the full 120 minutes and were Blocked by the factory timeout, with uncommitted work stashed
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-1325:BL-1325:unfinished-run
task: none
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
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

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
