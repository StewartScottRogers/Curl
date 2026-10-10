---
id: BL-1892
title: Act on client setenv in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1928]
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

- 2026-10-09 (lane 2): Measured the seam. `UpstreamCurlInvocation` carries no environment, and the composition `UpstreamConformanceTests.RunCurlAsync` calls (the dialing `CurlComposition.CreateRunner` in Curl.Console) reads no environment at all: it passes no `readEnvironmentVariable` to `CurlCommandRunner` and builds `new ProxySelector(_ => null)`. Un-screening `<setenv>` here alone would measure the cases with their variables ignored, so BL-1928 adds that seam first. Plan once it lands: an `EnvironmentVariables` dictionary on `UpstreamCurlInvocation` (optional constructor parameter, default empty); `UpstreamCaseRunner.RunScreenedAsync` fills it from `UpstreamTestPartBodies.Lines(testCase.Find("client", "setenv"))`, split at the first `=` (an empty value kept; expansion has already replaced %HOSTIP and the rest); `"setenv"` joins `UpstreamCaseScreening.ClientParts`; `RunCurlAsync` passes `name => invocation.EnvironmentVariables.GetValueOrDefault(name)`. Per-run injection leaves nothing to restore: a test shows the next run, without the variable, does not see it. Candidate cases: 1101 (no_proxy, http_proxy), 1034 and 1035 (LC_ALL), 1106, 1136, 1143, 1162, 1249-1257. The gap office's measuring tool also builds invocations; it is off limits to lanes, keeps compiling through the optional parameter, and an interactive session wires it.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1928: the conformance composition in Curl.Console reads no environment, so setenv cannot reach curl yet
- 2026-10-09: Backlog -> Doing.
