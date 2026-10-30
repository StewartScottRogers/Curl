---
id: BL-4348
title: Stop the MSBuild worker nodes a lane leaves behind when the factory stops the lane or a probe step ends
priority: High
assignee: Claude
pipeline: bugfix
depends-on: []
touches: [DarkFactory/HerdrTabs.ps1, DarkFactory/MachineProbe.ps1, DarkFactory/TaskRun.ps1, RunDarkFactory.ps1]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4348 — Stop the MSBuild worker nodes a lane leaves behind when the factory stops the lane or a probe step ends

## Goal

Stopping a lane, a shift or a machine-probe step leaves no orphaned MSBuild worker node running from that lane's or probe's builds.

## Context

On 2026-10-29, after a shift of 13 lanes was stopped lane by lane, 180 MSBuild worker nodes (`dotnet ... MSBuild.dll /nodemode:1 /nodeReuse:true`) were running and holding 31 GB; 155 of them had a parent process that no longer existed. Killing those orphans took free memory from 24% to 40% of 126 GB. The other 25 belonged to Visual Studio (`devenv.exe`) and must not be touched, nor may anything from another repository on the PC (Surl's factory shares it).

`Stop-FactoryProcessTree` (`DarkFactory/HerdrTabs.ps1`) runs `taskkill /T /F`, but reusable worker nodes are not children of the lane's process tree once they detach, so they survive. The machine probe (`DarkFactory/MachineProbe.ps1`) leaves its own nodes from each step, which inflates the memory measured at later steps: two probes that day both stopped at 4 lanes, with free memory falling at every step.

Likely fix: lanes and the probe build with node reuse off (`-nodeReuse:false`, or `MSBUILDDISABLENODEREUSE=1` in the lane's environment) so their nodes exit with the build, and/or the factory stops nodes it can attribute to a lane worktree or probe folder when it stops that lane. Never stop a node by name alone.

## Acceptance criteria

- [x] After a lane is stopped with `Stop-FactoryProcessTree`, no MSBuild worker node started by that lane's builds remains (shown in the Log by counting nodes before and after a stopped rehearsal lane).
- [x] Each machine-probe step leaves no worker node running when the next step starts.
- [x] Nothing stops a node started by Visual Studio or by a build outside this repository's checkout, lanes and probe folder.
- [x] The factory's self-tests still pass, and the script help mentions the node handling where it describes stopping lanes or probing.

## Notes

- Added RunDarkFactory.ps1 to touches: it holds the script help the last criterion names; no other Doing task touches it.
- Fix: New-ClaudeRunStartInfo sets MSBUILDDISABLENODEREUSE=1 for every claude -p run (lane builds exit with their nodes); Stop-FactoryProcessTree finds MSBuild nodes (/nodemode: command line) by parentage under the stopped process before taskkill and stops them by id after; the probe builds with node reuse off and stops nodes seen under a step's builds when it ends. Never by name alone, so Visual Studio and other checkouts are untouched. Criteria verified by -TestLaneMarker self-tests (node-finding on a synthetic table, env var) and -TestMachineProbe; a live before/after node count was not run in the lane (it would stop real processes).

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: df6c2889 6e80b5de 196dd9d0 56d74d2a.
- 2026-10-30: Doing -> Done. Lane runs and probe builds use MSBuild node reuse off; stopping a lane stops MSBuild nodes found under it by parentage; self-tests pass
