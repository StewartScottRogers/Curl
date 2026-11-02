---
id: BL-4486
title: Conformance: MQTT 5.0 over WebSocket (section 6) and TLS for mqtt5+ws:// and mqtt5s:// (Mqtt5 part 3 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4517, BL-4518, BL-4519]
touches: [Curl.Conformance.Extended.UnitLibrary/Mqtt5/Transports, Curl.Conformance.Extended.UnitTests/Mqtt5/Transports, Documentation/Conformance/Extended/Mqtt5-Transports.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4486 — Conformance: MQTT 5.0 over WebSocket (section 6) and TLS for mqtt5+ws:// and mqtt5s:// (Mqtt5 part 3 of 3)

## Goal

Curl's `mqtt5+ws://` and `mqtt5s://` transports are measured against OASIS MQTT 5.0 chapter 6 (WebSocket), the
RFC 6455 opening handshake and framing as they bind an MQTT client, and the TLS rules of chapter 5 that bind a
client, through the real curl command line; every such requirement is in a matrix mapped to a test, and every
failure is fixed or listed with its fix task.

## Context

Specifications: OASIS MQTT Version 5.0 (7 March 2019) chapter 6 ("Using WebSocket as a network transport":
binary frames, an MQTT packet across frames or several in one, the `mqtt` subprotocol) and chapter 5 where it binds
the client's use of TLS; RFC 6455 section 4.1 (client opening handshake: `Sec-WebSocket-Key`, `Upgrade`,
`Connection`, `Sec-WebSocket-Protocol`, checking `Sec-WebSocket-Accept` and the selected subprotocol), 5.1 to 5.5
(client frames masked, control frames, close). Builds on the reference broker BL-4517 wrote in
`Curl.Conformance.Extended.UnitLibrary/Mqtt5/`; put its WebSocket and TLS front ends in `Mqtt5/Transports/`, writing
the server side of RFC 6455 from the RFC (the shared `Curl.WebSocket` frame codec may be used for framing only).

Library: `Curl.Extended.Protocol.Mqtt5.UnitLibrary` (`WebSocket/Mqtt5WebSocketProtocolHandler`,
`Mqtt5WebSocketUpgrade` (path `/mqtt`), `Mqtt5WebSocketConnection`; ADR-1054). `mqtt5s://` is TLS over the
connector.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Mqtt5:MQTT-6.0.0-n`, `Mqtt5:RFC6455-<section>-<n>`), the matrix format and the `KnownFailures.txt` rule. One file
per case group; nothing added to `Harness/`. This task touches only its own conformance files so protocols run in
parallel: a fault in its own server or cases is fixed here, but a defect in the extension library is listed in
`KnownFailures.txt` with a filed High task `Fix Mqtt5 conformance: <requirement>` (pipeline feature; touches the
library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's
`.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Mqtt5-Transports.md` lists every chapter 6 statement, every RFC 6455 client
      MUST and SHOULD the handler relies on, and the chapter 5 client rules, each with an `Mqtt5:` ID mapped to a test
      or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/Transports/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: the opening handshake request, a wrong `Sec-WebSocket-Accept`, a
      reply without the `mqtt` subprotocol, a non-101 status, client frames masked and binary, a packet split over
      frames and two packets in one frame, a text frame from the server, a server close frame, and `mqtt5s://` over
      the TLS route with a refused certificate and with `-k`.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Mqtt5 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Mqtt5/Transports/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

## Log

- 2026-11-02: Created.
