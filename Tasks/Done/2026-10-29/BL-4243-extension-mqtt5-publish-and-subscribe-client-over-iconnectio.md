---
id: BL-4243
title: Extension Mqtt5: publish and subscribe client over IConnection for mqtt5 and mqtt5s
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4242]
touches: [Curl.Extension.Mqtt5.UnitLibrary/Client, Curl.Extension.Mqtt5.UnitTests/Client, Curl.Extension.Mqtt5.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4243 — Extension Mqtt5: publish and subscribe client over IConnection for mqtt5 and mqtt5s

## Goal

An `IProtocolHandler` in `Curl.Extension.Mqtt5.UnitLibrary` serves `mqtt5://` (port 1883) and `mqtt5s://` (port 8883, TLS through the connector curl's handlers receive) with curl's `mqtt://` behaviour lifted to MQTT 5: with `-d` it publishes the data to the URL's topic, otherwise it subscribes and writes what arrives to the output.

## Context

Split from BL-4115 (extension 14 of 63) for size by its first pass. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: OASIS MQTT Version 5.0. Builds on BL-4242's packet codec. Mirror curl's `mqtt://` user-facing behaviour (lib/mqtt.c: topic from the URL path, URL-decoded; publish when there is a request body; on subscribe, write each message as curl's handler does and stop when it stops) so the MQTT 5 schemes feel the same; curl's `mqtt://` itself stays 3.1.1 and is never changed. Never reference a `Curl.Protocol.*` library other than Abstractions, or another extension. Do not register the handler in any catalogue here: BL-4115 does that. Leave a seam (the flow takes the byte transport it speaks over) so BL-4244 can add `mqtt5+ws://` without changing this flow.

## Acceptance criteria

- [x] `mqtt5://host/topic` with a body sends CONNECT (3.1), waits for CONNACK (3.2), sends PUBLISH (3.3) and DISCONNECT (3.14); with no body it sends SUBSCRIBE (3.8), checks SUBACK (3.9) and writes received PUBLISH payloads; unit tests over a fake `IConnection` with recorded bytes.
- [x] A failure reason code in CONNACK, SUBACK or DISCONNECT ends the transfer with the exit code curl's `mqtt://` uses for the equivalent failure and the reason code's name in the error message; the mapping is recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [x] `--user` goes into CONNECT's User Name and Password (3.1.3.5, 3.1.3.6); a shared subscription URL path (`$share/name/filter`, 4.8.2) is subscribed as given.
- [x] `mqtt5s://` runs the same flow over TLS from the connector the handler receives; unit test with a fake connector.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4115's lane 6 prerequisite pass.
- Plan: `Client/Mqtt5ProtocolHandler` connects (TLS for `mqtt5s` through `ConnectTarget.UseTls`, ports 1883/8883) and runs `Mqtt5ClientFlow` over the `IConnection` it gets; the flow takes the transport, so BL-4244 can hand it a WebSocket-carrying connection for `mqtt5+ws://`. `Mqtt5PacketChannel` frames whole packets with BL-4242's `Mqtt5PacketReader`.
- Decisions (ADR-1049; ADR-1045 was taken on the shared branch by the OAuth2 lane): every failure reason code in CONNACK, SUBACK or DISCONNECT ends with exit 8, as curl's `mqtt://` does for a refused CONNACK or bad SUBACK, message `MQTT 5 <packet> reason code 0x<hex> <name>`; only the PUBLISH payload is written (MQTT 5 properties would be noise); CONNECT asks Keep Alive 0, and honouring a broker's Server Keep Alive is filed as BL-4261.
- Added `Curl.Extension.Mqtt5.UnitLibrary/CLAUDE.md` to touches: it said the library had no handler yet; no task in Doing on the shared branch names it.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Mqtt5.UnitLibrary` reports 0 failing members; `dotnet build` clean; fast tests green (Mqtt5 tests 157 passed). The Linux/macOS criterion is met by keeping the tests platform-neutral (in-memory fakes only).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2ec72062 fc4e9290 b32bcc34 b030ffdd 14de81cc.
- 2026-10-29: Doing -> Done. mqtt5:// and mqtt5s:// publish with -d and subscribe otherwise over MQTT 5, reason codes mapped to curl's exit codes
