---
id: BL-935
title: Write curl's -v and --trace lines for TELNET and MQTT transfers after connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-935 — Write curl's -v and --trace lines for TELNET and MQTT transfers after connect

## Goal

`telnet://` and `mqtt://` (and `mqtts://`) transfers write curl 8.21.0's `-v` lines and `--trace`/`--trace-ascii` blocks after connect: TELNET's option negotiation and data, MQTT's packets, not only the connect lines they write today.

## Context

- Audit 2026-09-29, part B: an archived task made both handlers pass `context.Events` to their `ConnectTarget` (`TelnetProtocolHandler.cs` line ~111, `MqttProtocolHandler.cs` line ~149), so the connect lines appear; after connect neither reports any event.
- curl's `lib/telnet.c` writes option negotiation to the verbose output when `-v` is on; which lines, in which format, and whether curl 8.21.0 dumps TELNET and MQTT bytes as data or header blocks, must be measured, not assumed.
- Where: `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs`; `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs`, `MqttPacketReader.cs`, `MqttPackets.cs`, `MqttTransferMessages.cs` (keep measured texts there).
- Measure first with `Record-CurlExchange.ps1` (default TCP mode with `-Response` holding the server's bytes, or `-Script` for a multi-step exchange, `-Tls` for `mqtts`): `-v`, `--trace-ascii -` and `--trace -` for a TELNET session where the server sends `IAC DO TERMINAL-TYPE` and `IAC WILL ECHO` then text and closes; `telnet://host -t TTYPE=vt100`; an MQTT subscribe (`mqtt://host/topic`) receiving one PUBLISH; an MQTT publish (`-d payload`); a CONNACK refusal. Copy output into Notes with curl's version and build before pinning text.

## Acceptance criteria

- [ ] Measured output for the five cases is copied into Notes.
- [ ] `Curl.Protocol.Telnet.UnitTests` and `Curl.Protocol.Mqtt.UnitTests` pin, through a recording `ITransferEvents`, every measured event after connect, in curl's order.
- [ ] Existing tests in both projects pass unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Telnet.UnitLibrary` and `Curl.Protocol.Mqtt.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
