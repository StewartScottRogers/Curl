---
id: GF-0080
title: --unix-socket cases throw in the gap harness: its dialer sends the Unix path to port 1, not to the sws emulation
area: behaviour
platform: windows
key: behaviour:harness-unix-socket-dial-misses-sws-port
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0304
closed: 2026-10-15_0543
regression: false
items: [behaviour:test1435, behaviour:test1436]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task: BL-2271
tasks: [BL-2271]
---
# GF-0080 - --unix-socket cases throw in the gap harness: its dialer sends the Unix path to port 1, not to the sws emulation

## Summary

Curl differs from upstream curl in behaviour: --unix-socket cases throw in the gap harness: its dialer sends the Unix path to port 1, not to the sws emulation.

## Evidence

behaviour:test1435 and test1436 expected 'upstream test<N> passes', actual 'curl threw IOException: The in-memory server refused the connection: Failed to connect to log/server/http.sock:1 after 0 ms: Could not connect to server'. Both run '--unix-socket %HTTPUNIXPATH http://<name>.example.com/<N>' against the http-unix server. Gap/Tools/Measure-UpstreamCases.cs InMemoryServerTcpDialer.DialUnixSocketAsync dials new ConnectTarget(address.Path, 1, false), while Curl.Conformance.UnitTests/InMemoryServerTcpDialer.cs (BL-2229) dials UpstreamCaseRunner.HttpPort so the path reaches the sws emulation (its test UnixSocket_ToTheHttpUnixPath_ReachesTheSwsStandIn). Curl itself is not at fault. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- '<release>/tests/data' $env:TEMP/raw.json 1435

## Suggestion

Give Curl.Conformance.UnitLibrary one public way to dial the http-unix server, for example a ConnectTarget factory on UpstreamCaseRunner that pairs HttpUnixPath with HttpPort. Use it from Curl.Conformance.UnitTests' InMemoryServerTcpDialer, and have the gap office's copy in Gap/Tools/Measure-UpstreamCases.cs call it instead of port 1 (that one-line Gap change is office work, done interactively). Pin the factory with a test in Curl.Conformance.UnitTests.

## Measurements

- 2026-10-15_0304: 2 of 2 items are gaps.
- 2026-10-15_0543: 0 of 2 items are gaps.
- 2026-10-15_0751: 0 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.
- 2026-10-31_1828: 0 of 2 items are gaps.

## Log

- 2026-10-15_0304: Opened by gap-behaviour.
- 2026-10-15_0349: Filed BL-2271.
- 2026-10-15_0543: Closed: run 2026-10-15_0543 measured every item as match or excluded.
