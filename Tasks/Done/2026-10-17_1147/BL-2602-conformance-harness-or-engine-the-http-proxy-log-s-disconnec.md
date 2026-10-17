---
id: BL-2602
title: Conformance harness or engine: the http-proxy log's [DISCONNECT] for a CONNECT tunnel to %HTTPPORT under connection-monitor (test503)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2602 — Conformance harness or engine: the http-proxy log's [DISCONNECT] for a CONNECT tunnel to %HTTPPORT under connection-monitor (test503)

## Goal

test503's `<verify><proxy>` ends with the `[DISCONNECT]` line upstream's http-proxy logs when the libtest's tunnelled connection closes.

## Context

Found by BL-2584. With BL-2584's Proxy-Authorization fix (left in its stash), test503 through the ported `lib503` differs at `<verify><proxy>` line 6: expected `[DISCONNECT]\n`, got the end. The CONNECT goes to `%HTTPPORT`, so the tunnel is served by the same `SwsHttpServerConnection`, which switches `recording` to `TunnelRecording` on the CONNECT (`ServeCompleteRequests`), so `RecordDisconnectOnce` writes the close to the origin's log, not the proxy's. Real sws runs the http-proxy as its own server and logs `[DISCONNECT]` in its own log when the client closes. Also check whether the engine closes (disposes) the tunnel connection before the case is verified: the reply carries `swsclose`, and libcurl closes the connection at `curl_multi_cleanup`/`curl_easy_cleanup`. test1509 is the other `http-proxy` + `connection-monitor` case.

## Acceptance criteria

- [x] test503's `<verify><proxy>` and `<verify><protocol>` both match, each with its `[DISCONNECT]` line, when run with `CURL_UPSTREAM_CASE=503`.
- [x] A focused unit test in `Curl.Conformance.UnitTests` (or the engine's, if the fix is there) pins the fix.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 1): Fix in the harness, not the engine. `SwsHttpServerConnection` keeps the http-proxy's own recording (`proxyRecording`) when a `CONNECT` moves recording to the tunnel's, and `RecordDisconnectOnce` writes `[DISCONNECT]` into both, as upstream's http-proxy is its own sws logging the close (the tunnelled reply's `swsclose` closes both). The engine already closes the tunnel before verification: `<verify><protocol>` had its line. Pinned by `SwsHttpServerConnectorTests.ConnectionMonitor_TunnelThroughTheProxyPortClosed_RecordsADisconnectInBothLogs` and `ConnectionMonitor_SecondConnectInsideTheTunnel_StillRecordsOneDisconnectInEachLog`. Criterion 1 checked with BL-2584's Proxy-Authorization change applied temporarily (`ProxyCredentials` from `ProxyUserName`/`ProxyPassword`): `CURL_UPSTREAM_CASE=503` passed, both `<verify><proxy>` and `<verify><protocol>` with `[DISCONNECT]`; the change was reverted and stays BL-2584's, which now has nothing left to wait for. test1509 skips (`lib1509` not ported). Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 34955914 e9a7e0b8 3d836d5e.
- 2026-10-17: Doing -> Done. the http-proxy stand-in logs [DISCONNECT] when a CONNECT tunnel closes under connection-monitor (test503)
