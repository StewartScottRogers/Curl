---
id: BL-1903
title: Emulate upstream's IPv6 HTTP test server (%HOSTNIP, %HTTPNPORT) in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1903 — Emulate upstream's IPv6 HTTP test server (%HOSTNIP, %HTTPNPORT) in the case runner

## Goal

The runner serves the sws HTTP stand-in on an IPv6 loopback address (%HOSTNIP, %HTTPNPORT), so the 8 IPv6 HTTP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 8 cases are skipped for %HOSTNIP and %HTTPNPORT (upstream's HTTP-IPv6 server on ::1). Give both variables values, route ::1 and the second HTTP port to SwsHttpServerConnector, and check the resolver stand-in used in the tests (LoopbackOnlyDnsResolver) handles [::1]. Everything is in memory, so no case needs IPv6 from the machine. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] All 8 IPv6 HTTP cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %HOSTNIP, %HTTPNPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
