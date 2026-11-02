---
id: BL-4492
title: Conformance: Engine.IO v4 and Socket.IO v5 over WebSocket for socketio:// and socketios://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/SocketIo, Curl.Conformance.Extended.UnitTests/SocketIo, Documentation/Conformance/Extended/SocketIo.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4492 — Conformance: Engine.IO v4 and Socket.IO v5 over WebSocket for socketio:// and socketios://

## Goal

Curl's `socketio://` and `socketios://` client is measured against the Engine.IO v4 and Socket.IO v5 protocols over
the WebSocket transport through the real curl command line, every client requirement is in a matrix mapped to a
test, and every failure is fixed or listed with its fix task.

## Context

Specifications: Engine.IO protocol v4 (socket.io/docs/v4/engine-io-protocol and github.com/socketio/engine.io-protocol:
handshake `open` packet with `sid`, `pingInterval`, `pingTimeout`, `maxPayload`; packet types open, close, ping, pong,
message, upgrade, noop; the server-initiated heartbeat in v4; the WebSocket-only connection with
`/socket.io/?EIO=4&transport=websocket`) and Socket.IO protocol v5 (socket.io/docs/v4/socket-io-protocol and
github.com/socketio/socket.io-protocol: CONNECT with namespace and auth payload, CONNECT_ERROR, DISCONNECT, EVENT,
ACK, BINARY_EVENT and BINARY_ACK with attachments, packet encoding), plus the RFC 6455 client rules the handler
relies on. Binary packets the client does not support map to `n/a` or a failing case per the library's ADR.

Library: `Curl.Extended.Protocol.SocketIo.UnitLibrary` (`CLAUDE.md`: `SocketIoProtocolHandler`, `SocketIoUpgrade`,
`SocketIoSession`, `SocketIoPacket`, `SocketIoEmit`; ADR-1035). `-d` is the event array.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`SocketIo:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference server implements the protocols from the specifications
itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`.
This task touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is
fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix SocketIo conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/SocketIo.md` gives every requirement of Engine.IO v4, Socket.IO v5 and the
      RFC 6455 client rules relied on that binds a client a `SocketIo:` ID mapped to a test or to `n/a: <reason>`;
      the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/SocketIo/` run curl in process for `socketio://` and `socketios://`
      and assert the upgrade request, frames, stdout, stderr and exit code for at least: the `open` packet and its
      fields, the server ping answered with pong and a missed ping timing out on the injected clock, CONNECT to the
      default and a named namespace, CONNECT_ERROR, an EVENT emitted and one received, an ACK, a DISCONNECT from the
      server, a binary event, and a message over `maxPayload`.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix SocketIo conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/SocketIo/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
