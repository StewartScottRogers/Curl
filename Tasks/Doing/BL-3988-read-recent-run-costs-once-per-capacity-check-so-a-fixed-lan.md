---
id: BL-3988
title: Read recent run costs once per capacity check so a fixed-lane shift starts in seconds
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [DarkFactory/TaskBudget.ps1, DarkFactory/AutoLanesState.ps1]
requirement: none
created: 2026-10-28
completed:
---
# BL-3988 — Read recent run costs once per capacity check so a fixed-lane shift starts in seconds

## Goal

A `-Lanes N` shift starts its first lane within a minute of launch, instead of after about 12 minutes of reading run logs.

## Context

On 2026-10-28 a `-Lanes 12` shift launched at 14:55:49 and logged nothing until 15:07:39, spinning one core in Windows PowerShell 5.1. A first attempt was stopped by hand at about 14:55, taken for a hang.

Cause: `ShiftCoordinator.ps1` sizes a fixed shift with `Get-ShiftCapacity` before any lane starts. `Get-BoardCapacity` (`AutoLanesState.ps1`) calls `Get-TaskNotClaimableWhy` for every ready task the capacity line names (38 that day). Each call runs `Get-TaskCapSpentWhy` (`TaskBudget.ps1`), which calls `Get-RecentRunCosts`. That reads the 40 newest `BL-*.jsonl` logs (about 15 MB), which takes about 19 s, and the result is the same for every task in the check. `-Lanes Auto` makes the same calls, but in its step after the lanes are running, so it was never seen to block anything; it still stalls the coordinator for minutes every 15 minutes.

## Acceptance criteria

- [ ] One capacity check reads the recent run costs at most once, however many ready tasks it names (for example by computing the cap once and passing it to each task's check).
- [ ] A factory `-Test*` self-test shows `Get-RecentRunCosts` (or what replaces its reads) runs once for a check naming several ready tasks.
- [ ] Measured on this machine: `RunDarkFactory.cmd -NewTab -Lanes 12 ...` logs its `lanes: starting at` line within 60 s of launch, and the time is recorded in this task's Log.
- [ ] The cost-cap behaviour is unchanged: a task over its cap is still passed over with the same text.

## Notes

The 15:07 shift started 13 lanes, not 12, because lane 13 (a lane number left from the Auto shift before it) was adopted (`Get-FixedStartCount -Adopted`). That is by design and not part of this task.

## Log

- 2026-10-28: Created after the 14:55 `-Lanes 12` start took 12 minutes.
- 2026-10-28: Backlog -> Doing. Criteria at claim: bcca3c2f f10fcfb7 1fd801c2 dd05e472.
