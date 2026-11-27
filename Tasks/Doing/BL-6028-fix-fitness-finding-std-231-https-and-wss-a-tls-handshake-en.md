---
id: BL-6028
title: Fix fitness finding STD-231: https and wss: a TLS handshake ended by -m reports %{num_connects} 0 (and wss %{time_total} 0); curl reports 1 and the elapsed time
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019]
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitLibrary/TcpConnector.DirectConnect.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Engine.UnitTests/Std231Tests.cs, Curl.Networking.UnitTests/TcpConnectorStd231Tests.cs, Curl.Protocol.Abstractions.UnitTests/ConnectResultStd231Tests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6028 — Fix fitness finding STD-231: https and wss: a TLS handshake ended by -m reports %{num_connects} 0 (and wss %{time_total} 0); curl reports 1 and the elapsed time

## Goal

STD-231 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: https and wss: a TLS handshake ended by -m reports %{num_connects} 0 (and wss %{time_total} 0); curl reports 1 and the elapsed time. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs each, by the orchestrator) · **Area:** HTTPS/WSS write-out, Windows build.
- **Server:** a TCP listener on `127.0.0.1:29990` that accepts and never answers (a few lines of Python: `socket.bind/listen`, then `accept()` in a loop without reading or writing).
- **Repro:**
  - `curl -s -m 2 -o /dev/null -w "[%{exitcode} t=%{time_total} nc=%{num_connects}]\n" https://127.0.0.1:29990/`
  - the same with `wss://127.0.0.1:29990/`
- **Expected (REF 8.22.0):** `[28 t=2.0x nc=1]` for both.
- **Actual (.NET):** https `[28 t=2.01 nc=0]`; wss `[28 t=0.000000 nc=0]`.
- **Likely location:** the connect/TLS timeout path does not record the established TCP connection (and, for wss, the timings) before reporting; compare `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` (`StampTimingsAndUploadSize`).
- **Acceptance check:** both commands print `nc=1` and `t` of at least 2.

## Acceptance criteria

- [ ] both commands print `nc=1` and `t` of at least 2.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

2026-11-26 (lane 13): reproduced and measured; no code written. Needs `TcpConnector.cs`, which BL-6019
holds in Doing, so this waits on BL-6019.

Measured (Windows, curl 8.21.0 Schannel; a `TcpListener` on loopback that is never accepted from is
enough, no mock server; `-m 1` and `--connect-timeout 1`):

- TLS handshake timed out (`https`, `wss`, `ftps`, `imaps`): real `nc=1`, `t`=elapsed, `time_connect` 0,
  `time_appconnect` 0, pretransfer = starttransfer = total, empty `remote_ip`, `conn_id` 0.
  So only the connection count is missing: do not record end points or `ConnectTimings.Connected`.
- Curl now: `https` `nc=0`; `wss` `nc=0`, and `t=0.000000` when the connector's limit ends the connect
  (a failure with no report), `t`=elapsed when the `-m` cancellation escapes. `ftps`, `imaps` `nc=0` too.
- Controls where real curl keeps `nc=0`: TCP connect timeout (`10.255.255.1`), refused (`127.0.0.1:1`),
  unresolved name. So "a connect timed out" is not enough; the connector must say TCP was connected.
  The missing `t` for those is BL-6041.

Plan:

1. `TcpConnector.SecureWhenAskedAsync`: catch an `OperationCanceledException` from
   `AuthenticateTracingRecordsAsync`, mark it in `Data` (a key constant on `ConnectResult`), rethrow.
   The connect-limit catch copies the mark onto its `ConnectResult.Failed` (new `bool` property saying
   TCP connected before the failure). The proxy path's `CanceledAfterResolveException` wraps the
   cancellation in a new object, so carry the mark across it.
2. `EndPointRecordingConnector`: count one connection for a marked failed result, and for a marked
   cancellation (catch, record, rethrow). This covers the `-m` path.
3. `ConnectionEndPointRecorder.ReportOn`: `WithConnectionCount` dereferences `result.Report!`, so give a
   result with a count but no report one (that also stamps `wss`'s time_total). The count must apply
   even when `handlerCountsConnections` (`http`/`https` report 0 after a timed-out connect, built by
   `HttpTransferDeadline`).

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 45a3dc74 69116afe 3d836d5e.
- 2026-11-26: Doing -> Backlog. Needs Curl.Networking.UnitLibrary/TcpConnector.cs, held by BL-6019 in Doing; waits on BL-6019 (plan and measurements under Notes)
- 2026-11-27: Backlog -> Doing. Criteria at claim: 45a3dc74 69116afe 3d836d5e.
