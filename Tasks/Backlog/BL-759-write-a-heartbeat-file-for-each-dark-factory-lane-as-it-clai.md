---
id: BL-759
title: Write a heartbeat file for each dark factory lane as it claims, runs and integrates
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-762]
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-759 — Write a heartbeat file for each dark factory lane as it claims, runs and integrates

## Goal

During a shift, every runner keeps a heartbeat file up to date: each lane, and the single runner as lane `0`. The file is `<repo>.logs\lanes-<stamp>\lane-<n>.heartbeat.json` and holds one lane object in the ADR's `status.json` schema 1: lane, task, title, phase, step, task start time and heartbeat time. The coordinator (BL-760) can then publish every lane's state without any lane pushing.

## Context

- Stewart chose lane heartbeats on 2026-09-28 (option 2). The ADR from BL-762 fixes the lane object's fields, the phase names, the 60-second minimum refresh and the write-then-rename rule; follow it exactly.
- **Where things are in `RunDarkFactory.ps1`:**
  - `Get-LaneStatePath`/`Set-LaneState` (near line 1341) already write `lane-<n>.pid` and `lane-<n>.task` into `lanes-<stamp>`. Use `Get-LaneStatePath $Lane 'heartbeat.json'` for the path.
  - Phase points:
    - the main loop's claim (`Invoke-Claim`, near line 1646) and its `$claim.Wait` sleep;
    - `Invoke-TaskRun` (near line 1126);
    - `Invoke-Integrate` (near line 1265);
    - the waits for tokens (`Wait-ForNewSession`, `Wait-ForTokensByProbe`);
    - lane end (`Write-LaneSummary`).
  - `Invoke-TaskRun`'s read loop wakes every second, and `Write-Event`/`Get-ToolLabel` (near line 1041) name the current tool. Refresh the heartbeat from that loop at most once every 60 seconds, and whenever the tool label changes. The `step` field is the label's verb and detail, cut to 80 characters.
  - `Get-TaskTitle` gives the full title.
  - `$Stamp` is the shift stamp.
- **Time format:** UTC throughout, `(Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')`.
- **Writing the file:** write JSON with `ConvertTo-Json` to a temporary file in the same folder, then `Move-Item -Force` it over the target. A write failure is traced once and never stops the lane.
- **Test switch:** the script already has rehearsal switches (`-TestAlarm`, `-TestOutOfTokens`). Add `-TestHeartbeat` in the same style. It walks lane 1 through `starting`, `claim`, `run` (with a fake task `BL-000` and a step), `integrate` and `finished` in a temporary log root, prints each file's JSON, and exits 0. It must not touch the board, git or Claude.
- **Do not run a real shift to test.** A shift is running now, and lanes run the coordinator's copy of the script in `Z:\repos\Curl`. The change takes effect at the first shift after it reaches `work/dark-factory` and the coordinator's checkout pulls it.
- **PowerShell only**, Windows PowerShell 5.1 compatible like the rest of the script. No Python.

## Acceptance criteria

- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestHeartbeat` exits 0 and prints five JSON objects. Each parses with `ConvertFrom-Json` and has exactly the fields `lane`, `task`, `title`, `phase`, `step`, `taskStartedAt` and `heartbeatAt`.
  - The phases are, in order, `starting`, `claim`, `run`, `integrate` and `finished`.
  - `task`, `title` and `taskStartedAt` are `null` outside a held task.
  - Times match `^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$`.
- [ ] `[System.Management.Automation.Language.Parser]::ParseFile` reports no errors for `RunDarkFactory.ps1`.
- [ ] Reading the diff shows a heartbeat written:
  - at each phase point listed in Context, including the token waits (`tokens`) and the nothing-can-start-yet wait (`wait`);
  - from `Invoke-TaskRun`'s loop at least every 60 s and on each new tool label;
  - by the single runner as lane `0`.
- [ ] A heartbeat write that throws is caught and traced, and the lane carries on. A reader never sees a partial file, because of the temp-file-and-`Move-Item -Force` pattern.
- [ ] The script's header comment has a short "LIVE BOARD" paragraph saying lanes write heartbeat files and where, and naming the ADR.
- [ ] `git diff --stat` shows only `RunDarkFactory.ps1` changed outside `Tasks/`.

## Notes

## Log

- 2026-09-28: Created.
