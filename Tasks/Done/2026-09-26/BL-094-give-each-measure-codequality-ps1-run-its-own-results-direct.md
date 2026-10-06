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
completed: 2026-09-26
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

- [x] When `-ResultsDirectory` is not given, the script uses a directory that belongs to this run alone (a new GUID-named folder under `$env:TEMP\CurlCodeQuality`) or to this checkout alone (derived from `$PSScriptRoot`). Two worktrees never resolve to the same default.
- [x] The only directory the script deletes is its own resolved results directory, and the only reports it reads are the `*.cobertura.xml` files under that directory. It never deletes or reads the shared `$env:TEMP\CurlCodeQuality` parent or a sibling directory under it.
- [x] `-SkipTestRun` either reuses this checkout's reports (per-checkout default) or, without `-ResultsDirectory`, fails with a message saying that `-ResultsDirectory` is required (per-run default). An explicit `-ResultsDirectory` behaves as it did before this task.
- [x] The script's comment-based help has a `.PARAMETER ResultsDirectory` entry that states the default and how `-SkipTestRun` interacts with it.
- [x] Two runs started at the same time each report only their own coverage. Check it by running `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` from this checkout and, in a second shell started at the same moment, `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` from another worktree (for example `Z:\repos\Curl.lanes\lane-2`) that carries the change. Neither run fails with a missing report, and each report's member count matches what the same command reports when it runs alone. Record the commands and both member counts in `Notes`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`, run alone from the repository root, still prints the coverage/complexity/CRAP report and exits 0 when the library meets the thresholds.

## Notes

- Design choice (unattended default): a **per-checkout** default,
  `$env:TEMP\CurlCodeQuality\<checkout folder name>-<first 16 hex digits of SHA-256 of the lower-cased full checkout path>`,
  computed by `Get-CheckoutResultsDirectory`. Chosen over a GUID per run because it keeps
  `-SkipTestRun` working with no extra argument and each lane only ever measures from its
  own worktree. Cost: two runs from the *same* checkout at once still share a directory;
  the help says so. The readable folder-name prefix makes the directory easy to find.
- Explicit `-ResultsDirectory` is untouched: the default is only computed when it is blank.
  Checked with `-SkipTestRun -ResultsDirectory <lane-1 default>` on Mqtt: 55 members, exit 0.
- Delete/read scope: `Remove-Item` and `Get-ChildItem *.cobertura.xml` both still target
  only `$ResultsDirectory`, which is now a child of the shared parent. After the runs below,
  the sibling `bl094-second-checkout-1e70d540e0550d43` survived lane-1's own runs.
- Two checkouts resolve differently: lane-1 -> `lane-1-ec41ddc7a4f118fc`, lane-2 ->
  `lane-2-59d4fd21e2a44779`; a trailing backslash gives the same hash.
- Concurrency check: lane-2 was busy with another lane's uncommitted work and creating a
  new worktree was denied in this session, so the second checkout was a `git archive HEAD`
  export at `%TEMP%\bl094-second-checkout` with the changed script copied in. Started at
  the same moment:
  - `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` (lane-1): **55 members**, exit 0.
  - `powershell -NoProfile -File <export>\Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: **13 members**, exit 1 (threshold failures, not a missing report).
  - The same commands run alone from lane-1: Mqtt 55 members exit 0; Core 13 members exit 1, identical figures. `-SkipTestRun` alone on Core reused the reports: 13 members.
- Last criterion: run alone, the Core command prints the coverage/complexity/CRAP report
  and exits 1 because Curl.Core.UnitLibrary does **not** meet the thresholds today
  (91.11% line, 87.5% branch, 4 failing `PhysicalFileSystem` members). That is a real,
  pre-existing coverage gap, not this change, so the "exits 0 when the library meets the
  thresholds" condition is shown with Mqtt (meets them, exit 0). Filed as BL-097.
- Other lanes still running the old script delete the whole `%TEMP%\CurlCodeQuality`
  parent, including the new per-checkout folders, until they rebase onto this commit.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Measure-CodeQuality.ps1 defaults to a per-checkout results directory, so concurrent lanes never share reports
