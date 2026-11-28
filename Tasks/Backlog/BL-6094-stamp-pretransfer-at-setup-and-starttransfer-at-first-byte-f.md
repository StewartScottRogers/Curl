---
id: BL-6094
title: Stamp pretransfer at setup and starttransfer at first byte for an extended protocol receive ended by -m (EXT-097, part 2)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6019, BL-6093]
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Core.UnitLibrary/MaxTimeWatchdog.cs, Curl.Core.UnitTests/MaxTimeWatchdogTests.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerTimingsTests.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerTests.cs, Curl.Engine.UnitTests/TransferPerformerTests.cs, Curl.Engine.UnitTests/TransferContextFactoryTests.cs, Curl.Engine.UnitTests/Ext097ReceiveTimingsTests.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-6094 — Stamp pretransfer at setup and starttransfer at first byte for an extended protocol receive ended by -m (EXT-097, part 2)

## Goal

An extended protocol receive that `-m` ends reports `%{time_pretransfer}` as the setup time
and `%{time_starttransfer}` as the time the first byte arrived, not the end of the transfer.

## Context

Part of fitness finding EXT-097 (outside review of 2026-11-26 07:04; the verbatim finding is
in `BL-6033`). Repro: a NATS server on 127.0.0.1:28422 (a PowerShell stand-in does: send
INFO, answer PING with PONG, send `MSG` after 2.5 s), then
`curl -sS -m 5 -u s3cret: -o o1.txt -w "[pt=%{time_pretransfer} st=%{time_starttransfer} t=%{time_total}]\n" nats://127.0.0.1:28422/wo9`.
Measured 2026-11-28: `[28 pt=5.012 st=5.012 t=5.012]`.

Cause: for a scheme whose handler does not time itself,
`EndPointReportingProtocolHandler.TimedReport` sets `RequestReady` to the end, and
`TransferPerformer.WithFirstByte` sets `FirstByteReceived` to the end. Fix it for extended
schemes only: curl's own schemes stamp both at the end (measured, BL-515). Take
`RequestReady` from the first connection's `TlsHandshakeCompleted`, falling back to
`Connected`. Take `FirstByteReceived` from the first write to the transfer's output, which
needs a first-write stamp on the output stream (the wrapped output of
`Curl.Core.UnitLibrary/MaxTimeWatchdog.cs`, or
`Curl.Engine.UnitLibrary/TransferContextFactory.cs`).

## Acceptance criteria

- [ ] The NATS receive repro prints `pt` under 1 and `st` greater than 2 and less than `t`.
- [ ] A focused unit test in `Curl.Engine.UnitTests` pins both stamps for an extended scheme, and one pins that a curl scheme still stamps both at the end (ADR-0482).
- [ ] `Measure-CodeQuality.ps1` reports no member this task changes or adds outside a gate.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-6033. Depends on BL-6093 because both change
`EndPointReportingProtocolHandler.cs`.

## Log

- 2026-11-28: Created.
