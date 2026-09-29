---
id: BL-928
title: Log DICT, Gopher, TELNET and MQTT transfer steps to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-928 — Log DICT, Gopher, TELNET and MQTT transfer steps to the diagnostic log

## Goal

The DICT, Gopher, TELNET and MQTT handlers write the diagnostic log (components `dict`, `gopher`, `telnet`, `mqtt`) from `ITransferContext.DiagnosticLog` for each transfer step, and set it on every `ConnectTarget` they build.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs` (builds a `ConnectTarget` near line 59; also wraps `ITransferContext`: forward `DiagnosticLog`) and `DictRequest.cs`; `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` (target near line 100); `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs` (target near line 111); `Curl.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs` (target near line 149), `MqttSession.cs`, `MqttPacketReader.cs`.
- What, per level: `error` the failure that ends the transfer with its `CurlExitCode`; `warning` input ignored (an unknown TELNET option refused, an MQTT packet type not expected); `info` the DICT command or Gopher selector sent, the TELNET session start and end, MQTT CONNACK return code, SUBSCRIBE or PUBLISH done, transfer end with bytes and ms; `verbose` each TELNET option negotiation (DO/DONT/WILL/WONT and option), each MQTT packet type and length.
- Credential-bearing path: `mqtt://user:s3cret@host/topic` (the CONNECT password is never logged).

## Acceptance criteria

- [ ] Each of `Curl.Protocol.Dict.UnitTests`, `Curl.Protocol.Gopher.UnitTests`, `Curl.Protocol.Telnet.UnitTests` and `Curl.Protocol.Mqtt.UnitTests` pins its `info` request line, one `error` naming its `CurlExitCode`, and that the `ConnectTarget` it builds carries the incoming `DiagnosticLog`; the MQTT tests also pin the no-secret test above and the TELNET tests a `verbose` option negotiation line.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- BL-934 and BL-935 (curl's own `-v`/`--trace` lines for these protocols) touch the same libraries; the board serialises them.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
