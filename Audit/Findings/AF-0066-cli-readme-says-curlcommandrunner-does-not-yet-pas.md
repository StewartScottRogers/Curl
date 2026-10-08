---
id: AF-0066
title: Cli README says CurlCommandRunner does not yet pass on Range, ResumeFrom, MaxFileSize, ConnectTimeout or MaxTime; it passes all five
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:Curl.Cli.UnitLibrary/README.md:CurlCommandRunner:false-statement
reproduction: none
task: BL-1685
tasks: BL-1685
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0066 - Cli README says CurlCommandRunner does not yet pass on Range, ResumeFrom, MaxFileSize, ConnectTimeout or MaxTime; it passes all five

## Summary

High finding from the truthfulness auditor at `Curl.Cli.UnitLibrary/README.md:6`: Cli README says CurlCommandRunner does not yet pass on Range, ResumeFrom, MaxFileSize, ConnectTimeout or MaxTime; it passes all five. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/README.md:6`

README.md line 6: 'it does not yet pass on `Range`, `ResumeFrom` or `MaxFileSize` (task BL-095), nor `ConnectTimeout` or `MaxTime` (tasks BL-123 and BL-124).' The code passes every one: Curl.Console/TransferContextFactory.cs:164 `MaxFileSize = options.MaxFileSize`; CurlCommandRunner.cs:3426 `ParseRange(options.Range)`; CurlCommandRunner.cs:3663-3686 pass options.ResumeFrom; CurlCommandRunner.cs:4621 `MaxTimeWatchdog.StartFromCommandLine(options.MaxTime, ...)`; CurlComposition.cs:979-982 `ConnectTimeoutOf(options)` uses options.ConnectTimeout and options.MaxTime. Requirements.md FR-007 and FR-015 say Curl.Console passes the range and max-filesize (BL-095), so the two documents disagree. An agent reading this README would re-implement wiring that exists. Phase 2: BL-095 is in Tasks/Done/2026-09-26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/README.md -SimpleMatch 'does not yet pass on'; Select-String -Path Curl.Console/TransferContextFactory.cs -SimpleMatch 'MaxFileSize = options.MaxFileSize'
```

- Expected: No README match (or a README that says the runner passes these options), alongside the TransferContextFactory match.
- Actual: README.md:6 matches 'does not yet pass on `Range`, `ResumeFrom` or `MaxFileSize` ...' while TransferContextFactory.cs:164 matches 'MaxFileSize = options.MaxFileSize'.

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
