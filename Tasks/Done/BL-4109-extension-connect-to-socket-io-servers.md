---
id: BL-4109
title: Extension: Connect to Socket.IO servers
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.SocketIo.UnitLibrary, Curl.Extension.SocketIo.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4109 — Extension: Connect to Socket.IO servers

## Goal

Curl provides `socketio://` and `socketios://` URLs: emit and receive events, as the extension `Curl.Extension.SocketIo.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 8 of 63. Specification: Socket.IO protocol v5 and Engine.IO v4. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.SocketIo.UnitLibrary` and `Curl.Extension.SocketIo.UnitTests` exist in `/Extensions/SocketIo/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `socketio://` and `socketios://` URLs: emit and receive events.
- [x] Unit tests check its wire behaviour against named sections of Socket.IO protocol v5 and Engine.IO v4, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 5): The first extension registered in ExtensionCatalogue.cs as a scheme handler. `SocketIoProtocolHandler` connects through curl's IConnector (TLS for socketios, ports 80/443 by default), upgrades to `/socket.io/?EIO=4&transport=websocket` (URL query appended, `Sec-WebSocket-Accept` checked), frames with the shared Curl.WebSocket.UnitLibrary codec, joins the URL path's namespace, emits the `-d` JSON array as an EVENT once connected, answers Engine.IO and WebSocket pings, acknowledges events that ask with `[]`, and writes each event received as its JSON array and a newline until DISCONNECT/close (ADR-1035; HTTP long-polling, binary events, a CONNECT auth payload and our own ping timeout are left out there, with why).
- touches widened: `ExtensionDescriptionCatalogue.cs` (its one description line, a shared list file) and `Curl.Console.UnitTests/CurlCompositionTests.cs`, whose list of handlers that receive the pooling connector now includes SocketIoProtocolHandler (no live task in Doing on origin/work/dark-factory named either).
- Checked: `curl --extensions` and `--ai-help extensions` list socketio; `CURL_EXTENSIONS=0 curl socketio://127.0.0.1:9/` prints `curl: (1) Protocol "socketio" not supported` and exits 1 (pinned by SocketIoExtensionTests through ProtocolDispatcher and ExtensionProtocolHandlers.For).
- Measure-CodeQuality: Curl.Extension.SocketIo.UnitLibrary 100% line and branch, 0 failing members; Curl.Extension.Composition.UnitLibrary 100/100, 0 failing. Fast tests: 59 projects, 46683 passed, 0 failed. Platform-neutral (recorded bytes only, no paths or OS text); CI on Linux and macOS is left to the shift's watch.
- Known limit: an exception other than a Socket.IO failure (cancellation by --max-time, an I/O error) leaves the connection to the engine's teardown rather than disposing it in the handler.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d29fe1c1 9bdef3f8 43a41d24 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. socketio:// and socketios:// connect to Socket.IO v5 servers over WebSocket, emit the -d event and write each event received
