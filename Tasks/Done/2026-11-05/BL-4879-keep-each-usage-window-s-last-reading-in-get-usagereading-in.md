---
id: BL-4879
title: Keep each usage window's last reading in Get-UsageReading instead of rescanning 40 logs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Usage.ps1, DarkFactory/AutoLanes.ps1]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-4879 — Keep each usage window's last reading in Get-UsageReading instead of rescanning 40 logs

## Goal

`Get-UsageReading` reports each usage window's newest known utilization until that window resets, without scanning up to 40 whole stream-json logs on every call.

## Context

- Found by the shift-end review of 2026-11-05 (6749e6524...8745bc48d): silent-failure-hunter and code-reviewer each reported a problem with the same lines.
- `DarkFactory/Usage.ps1:54-90` (`Get-UsageReading`, rewritten in BL-4874 / ADR-0130's amendment). Since November 2026, Claude Code's `rate_limit_event` names only one window (`rateLimitType`). The function scans the 40 newest `.jsonl` logs and treats any window it does not find there as 0%.
- **Wrong reading (silent-failure-hunter):** suppose the weekly window is at 96% and recent events are all about `seven_day`. The last `five_hour` report can then fall outside the 40 newest files. With 13 lanes that is roughly three runs per lane. The 5-hour window, really at 84%, then reads 0%, so `Get-UsageStop` never stops claiming at `-StopAtUsage`, and `-Lanes Auto` paces as if no tokens were used. The comment's assumption that an unreported window is not near its limit does not hold when another window is the one being reported.
- **Cost (code-reviewer):** the loop stops early only once both windows are seen. Most of the week one window is never reported, so every lane claim (`Get-UsageStop`) and every coordinator Auto step (`ShiftCoordinator.ps1:516`) runs `Select-String` over all 40 logs. Those logs are often megabytes each. Before the rewrite, one file usually sufficed.
- A suggested fix is to keep the last value seen for each window, with its reset time, in script scope or a small state file, and update it only from files written since the last scan. Drop a window only when its reset passes.

## Acceptance criteria

- [x] A `five_hour` reading seen once keeps its utilization in `Get-UsageReading` until its `resetsAt` passes, however many newer `seven_day`-only events follow (a `-TestAutoLanes` case in `DarkFactory/AutoLanes.ps1` proves it).
- [x] A repeated `Get-UsageReading` call with no new or changed log files reads no log file again (a `-TestAutoLanes` case or a measured note in the Log shows it).
- [x] A window never reported still reads 0 with `FiveHourReported`/`WeekReported` false, as now.
- [x] `RunDarkFactory.ps1 -TestAutoLanes` passes.

## Notes

Get-UsageReading now keeps a per-file cache (path, size, write time) and a per-process last-known reading per window (dropped when its resetsAt passes). Tests in AutoLanes.ps1 (-TestAutoLanes) prove the three behaviours; an unchanged repeat call read 0 files. Not committed to production C#, so no quality run applies.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: fc8f91a7 445c2cf9 92c52330 5b3c28b7.
- 2026-11-05: Doing -> Done. Get-UsageReading caches per-file reads and keeps each window until reset; -TestAutoLanes passes
