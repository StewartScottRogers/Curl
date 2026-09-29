---
id: BL-775
title: Report the connect -v lines for dict, gopher, telnet and mqtt by passing the transfer's events to the connect target
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-775 — Report the connect -v lines for dict, gopher, telnet and mqtt by passing the transfer's events to the connect target

## Goal

`curl -v` for `dict://`, `gopher://`, `gophers://`, `telnet://` and `mqtt://` prints the connector's connect lines (`Trying`, `Established connection`, `Connection timed out after N milliseconds`, the exit 7 lines) as curl 8.21.0 does.

## Context

- Found in BL-510: `TcpConnector` reports every `-v` connect line on `ConnectTarget.Events` (ADR-0100), but `DictProtocolHandler.ExecuteAsync`, `GopherProtocolHandler.CreateTarget`, `TelnetProtocolHandler` and `MqttProtocolHandler` build their `ConnectTarget` without `Events = context.Events`, so the target reports to `NoTransferEvents` and nothing prints. FTP, HTTP, IMAP, POP3, SMTP and WS already set it.
- Measured in BL-510 (curl 8.21.0, Schannel): `curl -v --connect-timeout 1 dict://10.255.255.1/d:x` prints `*   Trying 10.255.255.1:2628...`, `* Connection timed out after 1010 milliseconds`, `* closing connection #0`, then `curl: (28) ...`.

## Acceptance criteria

- [ ] Each of the four handlers sets `Events = context.Events` on its `ConnectTarget`, and a test per handler shows the target it passes to the connector carries the context's events.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
