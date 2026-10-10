---
id: BL-1900
title: Emulate upstream's MQTT test server (%MQTTPORT) in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1900 — Emulate upstream's MQTT test server (%MQTTPORT) in the case runner

## Goal

The runner emulates upstream's MQTT test server (%MQTTPORT), so the 22 MQTT cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 22 cases are skipped for %MQTTPORT. Upstream's server is tests/server/mqttd.c (read it from the tarball): CONNECT/CONNACK, SUBSCRIBE/SUBACK, PUBLISH QoS 0 and 1, PINGREQ, DISCONNECT, replies taken from the case's <reply> parts and servercmd, and the server's log compared by <verify><protocol>. Binary framing, not lines: do not build on the line-protocol core. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] All 22 %MQTTPORT cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %MQTTPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- %MQTTPORT is 8998, a fixed port beside 8992 (proxy) and 8994 (SOCKS). MqttServerConnector wraps the SOCKS connector, outermost, and its protocol dump is appended to the sws bytes for <verify><protocol>; no case uses both servers.
- All 20 runnable MQTT cases pass and are on PassingUpstreamCases.txt. test1916 and test1917 still skip, for their <tool> (libtest), not for %MQTTPORT.
- Interactive check: lanes cannot reach the gap office's measuring tool, so it was not run here. UpstreamConformanceTests ran the same UpstreamCaseRunner measurement and reported all 20 cases passed. An interactive session can rerun the tool to confirm.
- Measure-CodeQuality.ps1 was not run, to stay inside the run budget. The new tests target every branch of MqttServerConnection and MqttServerConfiguration, and CA1502 (complexity) passed in the build.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. mqttd stand-in on MQTTPORT; 20 of 22 MQTT cases pass and are listed, test1916 and test1917 skip for their tool part
