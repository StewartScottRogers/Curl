---
id: BL-5043
title: Map a refused SignalR negotiate status to the WebSocket schemes exit code
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRNegotiator.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRTransportException.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs, Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRNegotiatorTests.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRProtocolHandlerTests.cs, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5043 — Map a refused SignalR negotiate status to the WebSocket schemes exit code

## Goal

A SignalR negotiate request refused with HTTP 401 or 403 exits 22 like the port's other WebSocket schemes (socketio, mqtt5+ws), instead of exit 8, and `SignalR.md` states the mapping.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-035 (Low, Confirmed). Covers signalr and signalrs.

**Repro.**
1. Run an HTTP server on 127.0.0.1:47176 that answers every request with `HTTP/1.1 401 X` and `Content-Length: 0`.
2. `curl -sS -m 3 -d '{"type":1,"target":"A","arguments":[]}' signalr://127.0.0.1:47176/hub`
3. Control: `curl -sS -m 3 socketio://127.0.0.1:47176/`

**Expected.** Consistency with the port's other WebSocket-based schemes: socketio prints `curl: (22) Refused WebSocket upgrade: 401`. `Documentation/Conformance/Extended/SignalR.md` documents exit 8 only for a negotiate body that carries `error` or offers no WebSockets (TP-Negotiate-6/7), not for an HTTP status. The review's acceptance allows exit 22, or 67 for 401 as the brokers do for a refused login; The run decides: use 22 for both 401 and 403 (matching socketio, the control) unless the broker precedent in the docs says otherwise, and record the choice in `SignalR.md`.

**Actual.** `curl: (8) SignalR: The negotiate request failed with HTTP status 401.` and exit 8. A 403 behaves the same.

**Where to start.** The negotiate step: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/SignalRNegotiator.cs` line 65 (`throw new SignalRTransportException($"The negotiate request failed with HTTP status {head.StatusCode}.")`). Existing tests pin the current wording: `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRNegotiatorTests.cs` line 86 and `Client/SignalRProtocolHandlerTests.cs` line 162 (status 500 gives `SignalR: The negotiate request failed with HTTP status 500.`). Keep other statuses (for example 500) as they are unless `SignalR.md` says otherwise, and update only the tests for 401/403.

## Acceptance criteria

- [x] Tests in `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRNegotiatorTests.cs` assert a negotiate answered 401, and one answered 403, fail with exit 22 (the exception carries the exit code the handler reports), with a message naming the status.
- [x] A test in `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRProtocolHandlerTests.cs` asserts the transfer result for a 401 is exit 22.
- [x] Regression: negotiate answered 500 keeps its current exit code and message, and the TP-Negotiate-6/7 cases (`error` in the body, no WebSockets transport) still exit 8.
- [x] `Documentation/Conformance/Extended/SignalR.md` states the mapping (HTTP 401/403 on negotiate gives exit 22) in a new row, its "Covered by" naming the new tests.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `ScriptedConnector` in `Curl.Extended.Protocol.SignalR.UnitTests/Client`.
- `SignalR.md` is also in BL-5019's `touches`, so the board runs the two one at a time; no dependency is needed.
- Review acceptance check: the command exits 22 (or 67 for 401), and `SignalR.md` states the mapping.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: ea04755d 3f55b778 8d6f8aec df6f5c4d a0b49a29.
- 2026-11-12: Decided 22 for 401 and 403 (matches socketio); `SignalRTransportException` now carries an exit code and the handler reports it, so `touches` gained that file and `SignalRProtocolHandler.cs` (no other task held them). The SignalR.md row is `n/a:` because the matrix wants a `[SpecRequirement]` carrier and this is a port mapping, not a spec requirement; it names the unit tests. CI on Linux/macOS: change is platform-neutral.
- 2026-11-12: Doing -> Done. 401/403 on negotiate now exit 22; tests, docs, quality gates green
