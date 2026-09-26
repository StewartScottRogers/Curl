---
id: BL-094
title: Give each Measure-CodeQuality.ps1 run its own results directory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-09-26
completed:
---
# BL-094 — Give each Measure-CodeQuality.ps1 run its own results directory

## Goal

Two `Measure-CodeQuality.ps1` runs started at the same time, from two dark factory lanes, never delete or read each other's coverage reports.

## Context

Found while working BL-081 on 2026-09-26. `Measure-CodeQuality.ps1` (repository root)
defaults `-ResultsDirectory` to `$env:TEMP\CurlCodeQuality` (the
`if ([string]::IsNullOrWhiteSpace($ResultsDirectory))` block just after
`$repositoryRoot = $PSScriptRoot`). Every dark factory lane, a git worktree under
`<repo>.lanes\lane-<n>`, shares that folder. Unless `-SkipTestRun` is passed, the
script deletes the folder, runs `dotnet test` with Cobertura coverage into it, and
merges every `*.cobertura.xml` it finds there, keeping the highest complexity for each
method. When two lanes measure at once, one lane deletes or reads the other's reports.
In BL-081 the Mqtt library report listed 62 members instead of 55, with a stale
`BuildConnect` complexity from another lane's report. For now the workaround is to pass
a lane-private `-ResultsDirectory`.

Keep `-SkipTestRun` in mind. It reuses the reports that are already in the results
directory, so a default that makes a fresh GUID folder on every run would leave
`-SkipTestRun` nothing to reuse. Two designs keep it working:
- a default per checkout, for example `$env:TEMP\CurlCodeQuality\<a stable hash of $PSScriptRoot>`;
- a GUID folder for each run, with `-SkipTestRun` then requiring an explicit `-ResultsDirectory` and failing with a clear message when none is given.

Pick one and document it. An explicit `-ResultsDirectory` must behave exactly as it
does today.

## Acceptance criteria

- [ ] When `-ResultsDirectory` is not given, the script uses a directory that belongs to this run alone (a new GUID-named folder under `$env:TEMP\CurlCodeQuality`) or to this checkout alone (derived from `$PSScriptRoot`). Two worktrees never resolve to the same default.
- [ ] The only directory the script deletes is its own resolved results directory, and the only reports it reads are the `*.cobertura.xml` files under that directory. It never deletes or reads the shared `$env:TEMP\CurlCodeQuality` parent or a sibling directory under it.
- [ ] `-SkipTestRun` either reuses this checkout's reports (per-checkout default) or, without `-ResultsDirectory`, fails with a message saying that `-ResultsDirectory` is required (per-run default). An explicit `-ResultsDirectory` behaves as it did before this task.
- [ ] The script's comment-based help has a `.PARAMETER ResultsDirectory` entry that states the default and how `-SkipTestRun` interacts with it.
- [ ] Two runs started at the same time each report only their own coverage. Check it by running `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` from this checkout and, in a second shell started at the same moment, `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` from another worktree (for example `Z:\repos\Curl.lanes\lane-2`) that carries the change. Neither run fails with a missing report, and each report's member count matches what the same command reports when it runs alone. Record the commands and both member counts in `Notes`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`, run alone from the repository root, still prints the coverage/complexity/CRAP report and exits 0 when the library meets the thresholds.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
