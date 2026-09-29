---
id: BL-792
title: Let Measure-CodeQuality.ps1 measure while a test project has no tests yet
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-792 — Let Measure-CodeQuality.ps1 measure while a test project has no tests yet

## Goal

`powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <name>` produces its report when every test passes, even while a freshly scaffolded `.UnitTests` project holds no tests.

## Context

- Found in BL-656 (2026-09-28): `Curl.Kerberos.UnitTests`, `Curl.Protocol.Ldap.UnitTests`, `Curl.Protocol.Rtsp.UnitTests` and `Curl.Protocol.Smb.UnitTests` have no tests yet, so `dotnet test --filter "TestCategory!=Integration"` prints `No test matches the given testcase filter` for each and exits non-zero although nothing failed. `Measure-CodeQuality.ps1` then throws `dotnet test failed with exit code 1` before measuring. `-SkipTestRun` works around it.
- Tell a real failure (a `Failed!` summary line or a build error) from the zero-test case, for example by reading the summary lines, rather than ignoring the exit code outright.

## Acceptance criteria

- [ ] With an empty `.UnitTests` project in the solution and every other test passing, `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` prints its report and exits 0.
- [ ] A failing test still makes the script throw `dotnet test failed`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 4 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-792-lane-4-20260929-023709; start with git cherry-pick --no-commit factory/BL-792-lane-4-20260929-023709 and fix it.
