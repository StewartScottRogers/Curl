---
id: BL-2228
title: Stand in for upstream's dict and telnet servers and the TLS fronts of MQTT and gopher so cases 1450, 1452, 1640 and 1272 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2228 — Stand in for upstream's dict and telnet servers and the TLS fronts of MQTT and gopher so cases 1450, 1452, 1640 and 1272 run

## Goal

Add in-process stand-ins for upstream's `dict` server (`%DICTPORT`), `telnet` server (`%TELNETPORT`) and the TLS fronts of the MQTT (`%MQTTSPORT`) and gopher (`%GOPHERSPORT`) servers, so cases 1450, 1452, 1640 and 1272 run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had `needs-server:dict` 1, `telnet` 1, `mqtts` 1, `gophers` 1, and running `Curl.Conformance.UnitTests` on 2026-10-14 names these four by `%DICTPORT` (1450), `%TELNETPORT` (1452), `%MQTTSPORT` (1640) and `%GOPHERSPORT` (1272). The gap tool uses `Curl.Conformance.UnitLibrary`.

Follow the existing stand-ins: `ILineProtocolResponder` + `LineProtocolServerConnector` for dict and telnet (read `servers.pm` and `tests/server/` at curl-8_22_0 for which program serves each, their ports, and what each answers; ports must not clash with 8989-9009 already used, see the library `CLAUDE.md`), and `TlsRelayConnection` as in `MailTlsServerConnector` for the TLS fronts of `MqttServerConnector` and `GopherServerConnector` (certificate `test-localhost.pem`; the port has a value only when the caller names a certificate directory). Keep `EmulatedServers` lists and screening in step. If a server needs more than a lane run, deliver dict and the TLS fronts first and file the rest as a follow-up task.

## Acceptance criteria

- [ ] Each new connector has its own `...ConnectorTests` pinning greeting/answer bytes and recorded `ProtocolLog`/`ReceivedBytes`; the runner places them in the connector chain and `<verify><protocol>` includes their dumps in write order (`UpstreamProtocolDump`).
- [ ] Screening lets `dict`, `telnet`, `mqtts` and `gophers` cases run; cases 1450, 1452, 1640 and 1272 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
