---
id: BL-6081
title: https and wss: count the TCP connection of a timed-out TLS handshake in %{num_connects}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019]
touches: [Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitLibrary/TcpConnector.DirectConnect.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Networking.UnitLibrary/NumberedConnectFailure.cs, Curl.Engine.UnitTests/Std231Tests.cs, Curl.Networking.UnitTests/TcpConnectorStd231Tests.cs, Curl.Protocol.Abstractions.UnitTests/ConnectResultStd231Tests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-6081 — https and wss: count the TCP connection of a timed-out TLS handshake in %{num_connects}

## Goal

An `https` or `wss` transfer whose TLS handshake is ended by `-m` or `--connect-timeout` reports `%{num_connects}` 1, as curl does, and the `wss` one reports the elapsed `%{time_total}`.

## Context

Part of fitness finding STD-231 (outside review `Curl-Fitness-Review-2026-11-26T0704`); BL-6028 in `Tasks/Deferred` holds the finding verbatim, the measurements of real curl, the plan and the progress notes. Repro: a `TcpListener` on `127.0.0.1:29990` that is never accepted from, then `curl -s -m 2 -o /dev/null -w "[%{exitcode} t=%{time_total} nc=%{num_connects}]\n" https://127.0.0.1:29990/` and the same with `wss://`; real curl prints `[28 t=2.0x nc=1]` for both. The `https` time that is still 10 to 30 ms short of 2 is BL-6082.

## Acceptance criteria

- [ ] Both repro commands (`https` and `wss`, `-m 2`) print `nc=1`, and the `wss` one prints `t` of at least 2.
- [ ] A refused connect (`https://127.0.0.1:1/`) still prints `nc=0`.
- [ ] Focused unit tests in `Curl.Engine.UnitTests`, `Curl.Networking.UnitTests` and `Curl.Protocol.Abstractions.UnitTests` pin the behaviour (ADR-0482).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Networking.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary` reports no member outside a gate that this change added or altered.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-6028.

Continues the stashed work of BL-6028.

The stash holds the whole change and its three test files, built clean with the Abstractions, Networking and Engine fast tests green, and measured: `https` `nc=1` (`t=1.97..1.99`), `wss` `[28 t=2.02 nc=1]`, refused `nc=0`. Left: the quality run and the commit. The earlier quality run was still building after 80 minutes when the deadline came, so start it early.

## Log

- 2026-11-27: Created.
