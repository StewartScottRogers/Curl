---
id: BL-6093
title: Report remote_ip for an extended protocol when -m ends the TLS handshake (EXT-097, part 1)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019]
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Networking.UnitLibrary/NumberedConnectFailure.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Engine.UnitTests/Ext097Tests.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerTests.cs, Curl.Networking.UnitTests/TcpConnectorStd231Tests.cs, Curl.Networking.UnitTests/TcpConnectorTests.FailureTimings.cs, Curl.Protocol.Abstractions.UnitTests/ConnectResultStd231Tests.cs, Documentation/Planning/Decisions/ADR-1475-an-extended-protocol-reports-the-peer-of-a-tls-handshake-m-ends.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-6093 — Report remote_ip for an extended protocol when -m ends the TLS handshake (EXT-097, part 1)

## Goal

An extended protocol whose TLS handshake `-m` (or `--connect-timeout`) ends reports the
dialled server in `%{remote_ip}:%{remote_port}`, with `%{num_connects}` 1 and
`%{time_total}` at least the limit; curl's own schemes are unchanged.

## Context

Part of fitness finding EXT-097 (outside review of 2026-11-26 07:04; the verbatim finding is
in `BL-6033`). The code is already written and kept in the stashed work of BL-6033; what is
missing is the quality gate, the full fast suite and the commit. Design: ADR-1475 (in the
stash): the end points go on a failed transfer only when
`ReportsEndPointsOfFailedConnects` is set, which `EndPointReportingProtocolHandler` does for
a scheme not in `ManagedCurlVersion.Protocols`. Real curl 8.18.0 for https gave
`ip=:-1 nc=1`, so https keeps `ip` empty.

Repro: a listener on 127.0.0.1:28950 that accepts TCP and never answers;
`curl -sS -m 2 -o out.bin -w "[%{exitcode} t=%{time_total} ip=%{remote_ip}:%{remote_port} nc=%{num_connects}]\n" amqps://127.0.0.1:28950/%2F/q`.

`Measure-CodeQuality.ps1` for the three libraries did not finish within the earlier run while
other lanes were building: run it in the background, narrowed with `-Library`.

## Acceptance criteria

- [ ] The amqps repro prints `t` at least 2, `ip=127.0.0.1:28950` and `nc=1`.
- [ ] The same command against `https://127.0.0.1:28950/` still prints an empty `ip` with port -1.
- [ ] Focused unit tests in `Curl.Engine.UnitTests`, `Curl.Networking.UnitTests` and `Curl.Protocol.Abstractions.UnitTests` pin the corrected behaviour (ADR-0482).
- [ ] `Measure-CodeQuality.ps1` reports no member of `Curl.Engine.UnitLibrary`, `Curl.Networking.UnitLibrary` or `Curl.Protocol.Abstractions.UnitLibrary` outside a gate among those this task changes or adds.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-6033.
Continues the stashed work of BL-6033.

State at the split (2026-11-28): the Abstractions, Networking and Engine fast tests passed
(1648, 5029 and 3276 tests); amqps, mqtt5s and grpcs gave
`[28 t=2.02 ip=127.0.0.1:28950 nc=1]`, as did `--connect-timeout 1`.

## Log

- 2026-11-28: Created.
