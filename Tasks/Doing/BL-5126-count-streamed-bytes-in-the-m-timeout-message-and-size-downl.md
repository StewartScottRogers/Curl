---
id: BL-5126
title: Count streamed bytes in the -m timeout message and size_download for socketio (EXT-036, socketio half)
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs, Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoProtocolHandler.cs, Curl.Extended.Protocol.SocketIo.UnitTests/SocketIoSessionTests.cs, Curl.Extended.Protocol.SocketIo.UnitTests/SocketIoProtocolHandlerTests.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-Realtime.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5126 — Count streamed bytes in the -m timeout message and size_download for socketio (EXT-036, socketio half)

## Goal

A `-m` timeout on a Socket.IO (`socketio://`, `socketios://`) listening transfer reports the bytes written (`with 17 bytes received`) and `%{size_download}` equals them, as it already does for nats and signalr.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-036 (Low, Confirmed, 3 runs each), re-reported as CHANGED: the earlier report's fix (task BL-5044, `Tasks/Done/2026-11-13/BL-5044-count-streamed-bytes-in-the-m-timeout-message-and-size-downl.md`) covered nats and signalr only. The CoAP observe half is a separate task (`Curl.Extended.Protocol.Coap`).

**Repro.** Run an Engine.IO v4 mock over WebSocket on 127.0.0.1:27035 (PowerShell or a C# file-based app, never Python). On upgrade it sends `0{"sid":"s1","upgrades":[],"pingInterval":25000,"pingTimeout":20000,"maxPayload":1000000}`; on `40/stay,` it answers `40/stay,{"sid":"x1"}` and `42/stay,["news",{"a":1}]`, then stays silent. Run `curl -sS -m 1.6 socketio://127.0.0.1:27035/stay -w '|size=%{size_download}\n'`.

**Expected.** `CurlSurface-Transfer-Realtime.md`: `%{size_download}` is the bytes written; nats prints `(28) ... with 3 bytes received` and `size=3`, signalr `with 16 bytes received` and `size=16`. socketio should report 17 bytes (`["news",{"a":1}]` plus the newline).

**Actual.** socketio prints `["news",{"a":1}]`, then `curl: (28) Operation timed out after 1614 milliseconds with 0 bytes received` and `|size=0`, exit 28 (with `-o`, the file holds 17 bytes).

**Where to start.** `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs`: `WriteEventAsync` (about line 180) writes the line and does `written += line.Length` but never reports it. Do what `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs:136-137` does (`progress.ReportDownloaded(bytesWritten, null)` after each line; it takes an `ITransferProgress` constructor parameter), and see BL-5044's change to the nats and signalr sessions for the pattern. The session is built in `SocketIoProtocolHandler.cs` line 123 from the `ITransferContext`; pass the context's progress reporter (`ITransferContext` exposes it beside `Events`). That one-line change in `SocketIoProtocolHandler.cs` belongs in this task: add it to `touches`.

## Acceptance criteria

- [ ] A new test in `Curl.Extended.Protocol.SocketIo.UnitTests/SocketIoSessionTests.cs` (or a new `SocketIoSessionProgressTests.cs`) proves that after one event `["news",{"a":1}]` the progress reporter has been told 17 downloaded bytes, and that a second event raises the running total (it never resets).
- [ ] A test proves that a `-m` timeout after one event surfaces as `CurlExitCode.OperationTimedOut` (28) with the message ending `with 17 bytes received`, via the handler path used by the existing `SocketIoProtocolHandlerTests`.
- [ ] The existing Socket.IO tests stay green, including ack events and a transfer with no events (still `0 bytes received`).
- [ ] The `CurlSurface-Transfer-Realtime.md` rows for the timeout message and `%{size_download}` cover socketio, "Covered by" naming a test from this task.
- [ ] The tests are platform-neutral (no real socket).
- [ ] `dotnet build Curl.Extended.Protocol.SocketIo.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: the socketio command prints `... with 17 bytes received` and `|size=17`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 7d828870 820c3dd3 b7c3b215 f92474a6 d6cd4299 84a3704f.
