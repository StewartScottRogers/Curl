---
id: BL-1385
title: Fix AF-0042: BL-1325 and BL-1360 each ran the full 120 minutes and were Blocked by the factory timeout, with uncommitted work stashed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1385 — Fix AF-0042: BL-1325 and BL-1360 each ran the full 120 minutes and were Blocked by the factory timeout, with uncommitted work stashed

## Goal

The defect the audit office reported as AF-0042 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0042 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0042-bl-1325-and-bl-1360-each-ran-the-full-120-minutes.md`.

Location: `logs/DarkFactory-20261003-061205-L2.log:11`

Location: `logs/DarkFactory-20261003-061205-L2.log:11`

BL-1325 (lane 2): claim 08:03:49, test 22/22 pass at 08:48 (40 min after the build), quality ok 09:33, stash 'uncommitted work kept' at 10:03:53, BLOCKED 10:04:23 'dark factory timed out after 1...'. BL-1360 (lane 7): claim 11:05:15, test 40/40 pass 11:11:58, quality steps up to 12:48:24, stash 13:05:18, BLOCKED 13:05:30, also a timeout. Both had passing tests early. About 80 minutes went on the quality and verify steps, and the work was parked in a stash rather than finished. The cause is a timeout, not a crash or a usage limit.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path ..\logs\DarkFactory-20261003-061205-L2.log,..\logs\DarkFactory-20261003-061205-L7.log -Pattern 'BL-1325|BL-1360' | Select-String -Pattern 'claim|stash|BLOCKED'
```

- Expected: Each task ends DONE well under 120 minutes.
- Actual: Both end BLOCKED at about 120 minutes with a stash.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
