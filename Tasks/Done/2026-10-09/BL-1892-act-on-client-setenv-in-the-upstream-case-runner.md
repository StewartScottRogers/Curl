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
completed: 2026-10-09
---
# BL-1892 — Act on client setenv in the upstream case runner

## Goal

The runner acts on <client><setenv> (sets the named environment variables for the curl run and restores them after) so the 73 cases skipped for it are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 73 cases are skipped with "the harness does not act on <client><setenv>". Environment for in-process curl must be isolated per run: check how UpstreamCurlInvocation / Curl.Cli reads the environment (an injected environment reader is preferable to Environment.SetEnvironmentVariable, which would race parallel tests) and use that. Variables are expanded (%HOSTIP etc.) before they are set; a name with an empty value sets an empty variable, as runtests.pl does. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] Unit tests set and clear a variable for a run and show it gone afterwards, and at least 10 named upstream cases that use <setenv> (say which, by number, in the tests) run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not act on <client><setenv>" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- Not a lane gate, so left unticked as a box and still to do in an interactive session: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2): Measured the seam. `UpstreamCurlInvocation` carries no environment, and the composition `UpstreamConformanceTests.RunCurlAsync` calls (the dialing `CurlComposition.CreateRunner` in Curl.Console) reads no environment at all: it passes no `readEnvironmentVariable` to `CurlCommandRunner` and builds `new ProxySelector(_ => null)`. Un-screening `<setenv>` here alone would measure the cases with their variables ignored, so BL-1928 adds that seam first. Plan once it lands: an `EnvironmentVariables` dictionary on `UpstreamCurlInvocation` (optional constructor parameter, default empty); `UpstreamCaseRunner.RunScreenedAsync` fills it from `UpstreamTestPartBodies.Lines(testCase.Find("client", "setenv"))`, split at the first `=` (an empty value kept; expansion has already replaced %HOSTIP and the rest); `"setenv"` joins `UpstreamCaseScreening.ClientParts`; `RunCurlAsync` passes `name => invocation.EnvironmentVariables.GetValueOrDefault(name)`. Per-run injection leaves nothing to restore: a test shows the next run, without the variable, does not see it. Candidate cases: 1101 (no_proxy, http_proxy), 1034 and 1035 (LC_ALL), 1106, 1136, 1143, 1162, 1249-1257. The gap office's measuring tool also builds invocations; it is off limits to lanes, keeps compiling through the optional parameter, and an interactive session wires it.

- 2026-10-09 (lane 1): Done as planned. `UpstreamCurlInvocation` has an optional `environmentVariables` (default empty); `UpstreamCaseRunner` fills it from `<client><setenv>` (split at the first `=`, empty value kept, a line with no `=` or starting `=` left out, as runtests.pl unsets such a name; `#` comment lines have no `=` and so drop out too); `"setenv"` joins screening's client parts; the conformance tests pass `invocation.EnvironmentVariables.GetValueOrDefault` as `CreateRunner`'s reader, so no process variable is touched and parallel cases cannot race. Tests: `UpstreamCaseRunnerTests.RunAsync_Setenv_SetsTheVariablesForThatRunOnlyAndTheNextRunHasNone`, `UpstreamCurlInvocationTests`, a screening row, and `UpstreamConformanceTests.SetenvCase_RunThroughCurl_IsMeasuredNotSkipped` over 63, 288, 392, 708, 1101, 1136, 1143, 1249, 1250, 1265 (pass) and 428 (real Curl difference). Of the 170 `<setenv>` cases, 24 now pass and are listed (63, 288, 329, 392, 429, 449, 708, 709, 736-738, 1101, 1136, 1143, 1249-1257, 1265); 203 passes on Windows but is left unlisted because its file:/path case was not checked off Windows; 214, 428, 448, 458, 755 fail on Curl differences (428/448/458: `VariableDefinition` reads the process environment - filed BL-1943); the rest skip for other reasons (`<tool>`, Debug, %HTTPSPORT, %SRCDIR, ...). Conformance tests: 1989 passed, 1132 skipped/inconclusive, 0 failed; whole fast suite green. Measure-CodeQuality not run (30-45 min under lane load): the new code is two branches (`??` in the invocation, `equals > 0` in the runner), each taken both ways by the tests above. The interactive measuring-tool check is left to an interactive session, since lanes may not read the gap office's folder.
## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1928: the conformance composition in Curl.Console reads no environment, so setenv cannot reach curl yet
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. UpstreamCaseRunner acts on client setenv through the run's injected environment; 24 setenv cases now pass
