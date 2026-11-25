---
id: BL-5337
title: Count one connect for HTTP/2 transfers multiplexed on a shared connection in parallel mode
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5337 — Count one connect for HTTP/2 transfers multiplexed on a shared connection in parallel mode

## Goal

In parallel mode (`-Z`) over one multiplexed HTTP/2 connection, `%{num_connects}` is 1 for exactly the transfer that opened the connection and 0 for the others, as curl reports.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-087 (Low, Confirmed, 2 runs over TLS, 2 over h2c). Area: HTTP/2 multiplexing and write-out, Windows (reference REF2, a curl with HTTP/2).

**Repro.** Against an HTTP/2 TLS server on 127.0.0.1:31543: `curl -sk -Z https://127.0.0.1:31543/a https://127.0.0.1:31543/b https://127.0.0.1:31543/c -o NUL -o NUL -o NUL -w "%{url} %{num_connects}\n"`.

**Expected (REF2).** `/a 1`, `/b 0`, `/c 0` (one connection; PORT's `-v` also shows one `Established connection`).

**Actual (PORT).** `/a 1`, `/b 1`, `/c 1`.

**Where to start.** `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs`, `NumConnectsOf` and the connect counting near line 458: every transfer waiting on the pending multiplexed connection counts its connect.

## Acceptance criteria

- [x] New tests in `Curl.Engine.UnitTests` (a new file) prove that three transfers multiplexed on one HTTP/2 connection (stand-in connector and connection) report `num_connects` 1, 0, 0 (exactly one transfer reports 1, the one that opened the connection).
- [x] A test proves transfers that each open their own connection (HTTP/1.1, or HTTP/2 to different hosts) each still report 1, and a transfer that reuses an idle pooled connection still reports 0.
- [x] Tests are platform-neutral (stand-ins, no socket).
- [x] Hand check recorded in the Log: the repro prints exactly one `1` and two `0`. Compare with real curl built with HTTP/2 where available; the HTTP/2 server is a PowerShell or C# file-based app, never Python.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: exactly one transfer prints `1`; the others print `0`.

## Notes (BL-5337 run)

- Cause was not Finish.cs: the run shares one ConnectionEndPointRecorder across -Z transfers, and WithConnectionCount gave an HTTP transfer that reused a connection (report ConnectionCount 0) the connection another transfer opened. Fix: EndPointReportingProtocolHandler passes handlerCountsConnections for an http/https result that has a report, so the HTTP handler's own count stands. touches widened to those two files (no other task in Doing touches them).
- Tests: EndPointReportingProtocolHandlerSharedRecorderTests (1,0,0 over one recorder; own-connection transfers keep 1; non-HTTP and report-less failures still count from the recorder). The pooled reuse path already reported 0 (CurlCommandRunnerHttp2MultiplexingTests). Platform-neutral, no sockets.
- Hand check: Kestrel HTTP/2 file-based server (TLS ALPN and h2c); before the fix about half the runs printed 1 1 1; after, 8 runs each printed exactly one 1 and two 0. Quality run for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 9e2e30b7 c65d3ab9 37935ee1 e9ef5ef0 a0b49a29.
- 2026-11-16: Doing -> Done. -Z HTTP transfers sharing a connection report num_connects 1 for the opener and 0 for the rest
