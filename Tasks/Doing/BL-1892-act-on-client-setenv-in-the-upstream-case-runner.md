---
id: BL-1892
title: Act on client setenv in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1892 — Act on client setenv in the upstream case runner

## Goal

The runner acts on <client><setenv> (sets the named environment variables for the curl run and restores them after) so the 73 cases skipped for it are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 73 cases are skipped with "the harness does not act on <client><setenv>". Environment for in-process curl must be isolated per run: check how UpstreamCurlInvocation / Curl.Cli reads the environment (an injected environment reader is preferable to Environment.SetEnvironmentVariable, which would race parallel tests) and use that. Variables are expanded (%HOSTIP etc.) before they are set; a name with an empty value sets an empty variable, as runtests.pl does. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] Unit tests set and clear a variable for a run and show it gone afterwards, and at least 10 named upstream cases that use <setenv> (say which, by number, in the tests) run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not act on <client><setenv>" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
