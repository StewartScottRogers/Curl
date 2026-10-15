---
id: BL-2271
title: Close GF-0080: --unix-socket cases throw in the gap harness: its dialer sends the Unix path to port 1, not to the sws emulation
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2271 — Close GF-0080: --unix-socket cases throw in the gap harness: its dialer sends the Unix path to port 1, not to the sws emulation

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0080 (--unix-socket cases throw in the gap harness: its dialer sends the Unix path to port 1, not to the sws emulation), so a later gap analysis measures each of `behaviour:test1435`, `behaviour:test1436` as `match`.

## Context

- Finding: GF-0080, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1435`, `behaviour:test1436`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1435 and test1436 expected 'upstream test<N> passes', actual 'curl threw IOException: The in-memory server refused the connection: Failed to connect to log/server/http.sock:1 after 0 ms: Could not connect to server'. Both run '--unix-socket %HTTPUNIXPATH http://<name>.example.com/<N>' against the http-unix server. Gap/Tools/Measure-UpstreamCases.cs InMemoryServerTcpDialer.DialUnixSocketAsync dials new ConnectTarget(address.Path, 1, false), while Curl.Conformance.UnitTests/InMemoryServerTcpDialer.cs (BL-2229) dials UpstreamCaseRunner.HttpPort so the path reaches the sws emulation (its test UnixSocket_ToTheHttpUnixPath_ReachesTheSwsStandIn). Curl itself is not at fault. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- '<release>/tests/data' $env:TEMP/raw.json 1435

Suggestion, copied from the finding:

Give Curl.Conformance.UnitLibrary one public way to dial the http-unix server, for example a ConnectTarget factory on UpstreamCaseRunner that pairs HttpUnixPath with HttpPort. Use it from Curl.Conformance.UnitTests' InMemoryServerTcpDialer, and have the gap office's copy in Gap/Tools/Measure-UpstreamCases.cs call it instead of port 1 (that one-line Gap change is office work, done interactively). Pin the factory with a test in Curl.Conformance.UnitTests.

## Acceptance criteria

- [ ] `behaviour:test1435`: Curl answers what curl 8.22.0 answers, `upstream test1435 passes`, so the item measures `match`.
- [ ] `behaviour:test1436`: Curl answers what curl 8.22.0 answers, `upstream test1436 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
