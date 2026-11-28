---
id: BL-6081
title: https and wss: count the TCP connection of a timed-out TLS handshake in %{num_connects}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019]
touches: [Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitLibrary/TcpConnector.DirectConnect.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Networking.UnitLibrary/NumberedConnectFailure.cs, Curl.Engine.UnitTests/Std231Tests.cs, Curl.Networking.UnitTests/TcpConnectorStd231Tests.cs, Curl.Protocol.Abstractions.UnitTests/ConnectResultStd231Tests.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6081 — https and wss: count the TCP connection of a timed-out TLS handshake in %{num_connects}

## Goal

An `https` or `wss` transfer whose TLS handshake is ended by `-m` or `--connect-timeout` reports `%{num_connects}` 1, as curl does, and the `wss` one reports the elapsed `%{time_total}`.

## Context

Part of fitness finding STD-231 (outside review `Curl-Fitness-Review-2026-11-26T0704`); BL-6028 in `Tasks/Deferred` holds the finding verbatim, the measurements of real curl, the plan and the progress notes. Repro: a `TcpListener` on `127.0.0.1:29990` that is never accepted from, then `curl -s -m 2 -o /dev/null -w "[%{exitcode} t=%{time_total} nc=%{num_connects}]\n" https://127.0.0.1:29990/` and the same with `wss://`; real curl prints `[28 t=2.0x nc=1]` for both. The `https` time that is still 10 to 30 ms short of 2 is BL-6082.

## Acceptance criteria

- [x] Both repro commands (`https` and `wss`, `-m 2`) print `nc=1`, and the `wss` one prints `t` of at least 2.
- [x] A refused connect (`https://127.0.0.1:1/`) still prints `nc=0`.
- [x] Focused unit tests in `Curl.Engine.UnitTests`, `Curl.Networking.UnitTests` and `Curl.Protocol.Abstractions.UnitTests` pin the behaviour (ADR-0482).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Networking.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary` reports no member outside a gate that this change added or altered.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-6028.

Continues the stashed work of BL-6028.

The stash holds the whole change and its three test files, built clean with the Abstractions, Networking and Engine fast tests green, and measured: `https` `nc=1` (`t=1.97..1.99`), `wss` `[28 t=2.02 nc=1]`, refused `nc=0`. Left: the quality run and the commit. The earlier quality run was still building after 80 minutes when the deadline came, so start it early.

Finished from the stash (second run). Measured with this build against a never-accepted loopback listener, `-m 2`:
`wss` `[28 t=2.014..2.067 nc=1]` over 10 runs; `https` `nc=1`; refused `127.0.0.1:1` `nc=0` for `https` and `wss`.
The stashed change alone left `wss` at `t=1.76..1.99` on some runs, because its total was timed from the handler's
start, after `-m`'s. `EndPointReportingProtocolHandler` (added to `touches`; no other task in Doing names it) now
times an exit 28 whose handler reported no timings from `ITransferContext.OperationStarted` (ADR-1469). `https`
times itself and is left to BL-6082.
Quality: scored from the coverage of the three libraries' own `.UnitTests` (`-SkipTestRun`), a lower bound: no
failing member is one this change added or altered. `dotnet build` clean, fast tests green.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: f95c1f0d 0fe6442f 682d32cc 171fc27d 3d836d5e.
- 2026-11-27: Doing -> Done. https and wss count the TCP connection of a timed-out TLS handshake in num_connects (1), and wss times it from -m's start (ADR-1469).
