---
id: BL-1685
title: Fix AF-0066: Cli README says CurlCommandRunner does not yet pass on Range, ResumeFrom, MaxFileSize, ConnectTimeout or MaxTime; it passes all five
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1685 — Fix AF-0066: Cli README says CurlCommandRunner does not yet pass on Range, ResumeFrom, MaxFileSize, ConnectTimeout or MaxTime; it passes all five

## Goal

The defect the audit office reported as AF-0066 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0066 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0066-cli-readme-says-curlcommandrunner-does-not-yet-pas.md`.

Location: `Curl.Cli.UnitLibrary/README.md:6`

Location: `Curl.Cli.UnitLibrary/README.md:6`

README.md line 6: 'it does not yet pass on `Range`, `ResumeFrom` or `MaxFileSize` (task BL-095), nor `ConnectTimeout` or `MaxTime` (tasks BL-123 and BL-124).' The code passes every one: Curl.Console/TransferContextFactory.cs:164 `MaxFileSize = options.MaxFileSize`; CurlCommandRunner.cs:3426 `ParseRange(options.Range)`; CurlCommandRunner.cs:3663-3686 pass options.ResumeFrom; CurlCommandRunner.cs:4621 `MaxTimeWatchdog.StartFromCommandLine(options.MaxTime, ...)`; CurlComposition.cs:979-982 `ConnectTimeoutOf(options)` uses options.ConnectTimeout and options.MaxTime. Requirements.md FR-007 and FR-015 say Curl.Console passes the range and max-filesize (BL-095), so the two documents disagree. An agent reading this README would re-implement wiring that exists. Phase 2: BL-095 is in Tasks/Done/2026-09-26.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/README.md -SimpleMatch 'does not yet pass on'; Select-String -Path Curl.Console/TransferContextFactory.cs -SimpleMatch 'MaxFileSize = options.MaxFileSize'
```

- Expected: No README match (or a README that says the runner passes these options), alongside the TransferContextFactory match.
- Actual: README.md:6 matches 'does not yet pass on `Range`, `ResumeFrom` or `MaxFileSize` ...' while TransferContextFactory.cs:164 matches 'MaxFileSize = options.MaxFileSize'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 4 could not integrate: push kept being refused. The work is on branch factory/BL-1685-lane-4-20261007-111121; start with git cherry-pick --no-commit factory/BL-1685-lane-4-20261007-111121 and fix it.
