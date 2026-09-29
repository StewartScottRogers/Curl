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
completed: 2026-09-29
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

- [x] Each of `Curl.Protocol.Dict.UnitTests`, `Curl.Protocol.Gopher.UnitTests`, `Curl.Protocol.Telnet.UnitTests` and `Curl.Protocol.Mqtt.UnitTests` pins its `info` request line, one `error` naming its `CurlExitCode`, and that the `ConnectTarget` it builds carries the incoming `DiagnosticLog`; the MQTT tests also pin the no-secret test above and the TELNET tests a `verbose` option negotiation line.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- BL-934 and BL-935 (curl's own `-v`/`--trace` lines for these protocols) touch the same libraries; the board serialises them.
- Each library gained an internal `<Protocol>DiagnosticLog` on FTP's `FtpDiagnosticLog` pattern (BL-924): `IsEnabled` tested before any message is built, invariant culture, error line `failed with {ExitCode} ({int}): {message}`, end line `transfer finished|session ended: {bytes} bytes in {ms} ms` timed with `context.TimeProvider`. No new ADR: every choice follows ADR-0222 as FTP applied it.
- DICT: info `sent <command line>`. Context said the handler wraps `ITransferContext`; it does not, so there was nothing to forward - only the `ConnectTarget` carries the log. A `dict://user:s3cret@...` test also shows no secret is logged.
- Gopher: info `sent selector <selector>`.
- TELNET: info `session started to host:port`; verbose `recv|sent WILL|WONT|DO|DONT <option>` (named for BINARY, ECHO, SGA, TTYPE, NAWS, XDISPLOC, NEW-ENVIRON, else the number); warning `refused option <name> with WONT|DONT` for every refusal, not only unknown options, since a refusal of a known-but-unwanted option is input ignored too.
- MQTT: info `SUBSCRIBE topic` / `PUBLISH topic, n bytes`, `CONNACK return code n`, SUBSCRIBE/PUBLISH done; verbose `sent|received <TYPE>, remaining length n` - packet bodies are never logged, so neither the CONNECT credentials nor a payload appear; warning for each unexpected packet type.
- Coverage (Measure-CodeQuality.ps1 -SkipTestRun on each project's own cobertura run): 100% line and branch, 0 failing members, worst CRAP 10, for all four libraries. Tests: Dict 55, Gopher 55, Telnet 197, Mqtt 86; no existing test file changed.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. DICT, Gopher, TELNET and MQTT write the diagnostic log and pass it to their connect targets
