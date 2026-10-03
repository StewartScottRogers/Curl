---
id: BL-1356
title: Cover the eleven Curl.Console members below the quality gates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1356 — Cover the eleven Curl.Console members below the quality gates

## Goal

Every member of `Curl.Console` meets the quality gates: `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage, no method above cyclomatic complexity 10 and no CRAP score above 30.

## Context

- Found in BL-1287 (2026-10-03): `Measure-CodeQuality.ps1 -Library Curl.Console` reported 100% line, 99.06% branch and 11 failing members, none of them changed by BL-1287: `CurlCommandRunner.TransferAllGroupsAsync` (branch 85.71, complexity 14), `CurlCommandRunner.WriteProgressAsync` (91.67, 12), `CurlComposition.CreateProtocolHandlers` (83.33, 12), `UrlTransfer..ctor` (66.67, 12), `TransferCredentialLookup.CredentialsOf` (70), `HttpVersionMapping.ToHttpVersionPreference` (88.89), `CurlCommandRunner.RunAsync` (66.67), `CurlCommandRunner.TransferAllAsync` (66.67), `OutputFileOpenWarning.ReasonFor` (83.33), `TransferProgressRecorder.ReportUploaded` (75), `CurlCommandRunner.UrlOutputOf` (50).
- Complexity above 10 is split into private methods; uncovered branches get tests in `Curl.Console.UnitTests`. Thresholds in `CodeMetricsConfig.txt` stay as they are.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line, 100% branch and 0 failing members.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
