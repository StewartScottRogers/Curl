---
id: BL-4918
title: Send -H, -A, -b, -u and --oauth2-bearer on socketio and signalr handshakes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpRequestWriter.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRNegotiator.cs, Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4918 — Send -H, -A, -b, -u and --oauth2-bearer on socketio and signalr handshakes

## Goal

socketio and signalr handshakes carry User-Agent, custom headers, cookies and Authorization as the port's `ws://` does.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-009 (Medium, confirmed: reproduced at least twice).

- Repro: HTTP capture server on 18671. `curl -H "Authorization: Bearer abc" -A agentX -b c=1 -d "[\"e\"]" socketio://127.0.0.1:18671/`; `curl -H "Authorization: Bearer abc" -A agentX --oauth2-bearer TT -d "{\"target\":\"A\"}" signalr://127.0.0.1:18671/hub`; control `curl -H "Authorization: Bearer abc" -A agentX ws://127.0.0.1:18671/x`.
- Expected: `User-Agent: agentX`, the Authorization header and the Cookie are sent (ADR-1038 notes SignalR uses `Authorization: Bearer`).
- Actual: socketio's GET has only Host, Upgrade, Connection, Sec-WebSocket-Key, Sec-WebSocket-Version; signalr's negotiate POST only Host and `Content-Length: 0`.
- Where to start: The three files in touches.

## Acceptance criteria

- [ ] Both captured requests contain `User-Agent: agentX` and an Authorization header (signalr with `--oauth2-bearer TT` sends `Bearer TT`, the precedence documented in its conformance file).
- [ ] socketio's request also contains `Cookie: c=1`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 318f9b01 1e58b3fc a0b49a29.
