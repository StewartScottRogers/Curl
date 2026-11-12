---
id: BL-5030
title: Send -H, -A and -b on the mqtt5+ws WebSocket upgrade
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketUpgrade.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket/Mqtt5WebSocketUpgradeTests.cs, Documentation/Conformance/Extended/Mqtt5-Transports.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5030 — Send -H, -A and -b on the mqtt5+ws WebSocket upgrade

## Goal

The `mqtt5+ws://` WebSocket upgrade GET carries the `-H` headers, `-A` User-Agent and `-b` Cookie, as `ws://`, `socketio://` and `signalr://` do.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-033 (Medium, Confirmed).

**Repro.**
1. Run an HTTP capture server on 127.0.0.1:47160 that logs each request and answers `HTTP/1.1 403 X`.
2. `curl -sS -m 3 -H "Authorization: Bearer abc" -A agentX -b c=1 mqtt5+ws://127.0.0.1:47160/t`

**Expected.** The port's own `ws://`, `socketio://` and `signalr://` all send `User-Agent: agentX`, `Cookie: c=1` and `Authorization: Bearer abc` on the upgrade GET. Brokers behind an authenticating gateway need these headers.

**Actual.** The GET carries only `Host`, `Upgrade`, `Connection`, `Sec-WebSocket-Key`, `Sec-WebSocket-Version` and `Sec-WebSocket-Protocol: mqtt`, and curl prints `curl: (22) Refused WebSocket upgrade: 403`.

**Where to start.** `Curl.Extended.Protocol.Mqtt5.UnitLibrary/WebSocket/Mqtt5WebSocketUpgrade.cs` lines 55-61, where the request is a fixed string. See how the socketio handler builds the same fields (`Curl.Extended.Protocol.SocketIo.UnitLibrary/HandshakeHeaderFields.cs`, `SocketIoUpgrade.cs`) and follow it, including its rules for a `-H` that names a header the upgrade already sets.

## Acceptance criteria

- [x] Tests in `Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket/Mqtt5WebSocketUpgradeTests.cs` assert the captured GET contains `User-Agent: agentX`, `Authorization: Bearer abc` and `Cookie: c=1` for `-A agentX -H "Authorization: Bearer abc" -b c=1`.
- [x] A test asserts that with no `-A` the default curl User-Agent is sent, as `ws://` does.
- [x] Regression: `Host`, `Upgrade`, `Connection`, `Sec-WebSocket-Key`, `Sec-WebSocket-Version` and `Sec-WebSocket-Protocol: mqtt` are still sent exactly once each, and a refused upgrade still exits 22 with `Refused WebSocket upgrade: 403`.
- [x] `Documentation/Conformance/Extended/Mqtt5-Transports.md` gains a row stating the upgrade carries `-H`, `-A` and `-b`, its "Covered by" naming the new tests.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Done: `Mqtt5WebSocketUpgrade.Request` now takes the transfer context and sends Authorization (Bearer, else Basic), User-Agent (curl default `curl/8.22.0` as socketio), Cookie, then each `-H`; a `-H` naming a field the upgrade sets (Host, Upgrade...) replaces or removes it so it is still sent once, as curl does. Logic is copied (not shared) because the Mqtt5 library may not reference SocketIo. Quality run: 100% line and branch, 0 failing. CI on Linux and macOS: tests are platform-neutral.

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `Curl.Extended.Protocol.Mqtt5.UnitTests/WebSocket` stand-ins.
- Review acceptance check: the captured GET contains `User-Agent: agentX`, `Authorization: Bearer abc` and `Cookie: c=1`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 67c358bd 1506b1f6 1768b93d b8619031 a0b49a29.
- 2026-11-12: Doing -> Done. mqtt5+ws upgrade sends -H, -A, -b
