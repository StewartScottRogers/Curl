---
id: BL-2178
title: Close GF-0072's test3225: the sws stand-in records [DISCONNECT] for every monitored connection that closes
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2178 — Close GF-0072's test3225: the sws stand-in records [DISCONNECT] for every monitored connection that closes

## Goal

Upstream test3225 (curl 8.22.0) passes through Curl in the conformance harness, so gap item `behaviour:test3225` of GF-0072 measures `match`.

## Context

- Split from BL-2164. Curl already behaves as curl does for test3225: with keep-logs its stdout is `connects=1/connects=1/` (two connections, host-a and host-b at one `--resolve` address) and both are left intact and closed at exit.
- The verdict still fails, `<verify><protocol> differs at byte 201 (line 12): expected [DISCONNECT], got the end`, because `Curl.Conformance.UnitLibrary/SwsServerRecording.cs` keeps one server-wide monitor flag that `RecordDisconnect` clears after the first close, so the second connection's close is not recorded. Upstream expects one `[DISCONNECT]` per monitored connection (`connection-monitor` servercmd).
- Make the monitor record a close for every connection that served a request while `connection-monitor` was on (per connection, as sws's per-request `connmon`), and check the cases that pass today with `connection-monitor` still pass.
- Reproduce: `CURL_UPSTREAM_CASE=3225 dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"`.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=3225 dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes.
- [ ] A focused `SwsHttpServerConnectorTests` test pins two monitored connections closing as two `[DISCONNECT]` lines.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
