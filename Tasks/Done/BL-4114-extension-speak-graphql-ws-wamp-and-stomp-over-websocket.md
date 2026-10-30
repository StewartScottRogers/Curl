---
id: BL-4114
title: Extension: Speak graphql-ws, WAMP and STOMP over WebSocket
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4253]
touches: [Curl.Extension.WebSocketSubprotocols.UnitLibrary, Curl.Extension.WebSocketSubprotocols.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4114 — Extension: Speak graphql-ws, WAMP and STOMP over WebSocket

## Goal

Curl provides an option naming a WebSocket subprotocol and framing messages for it, as the extension `Curl.Extension.WebSocketSubprotocols.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 13 of 63. Specification: graphql-transport-ws protocol, WAMP basic profile, STOMP 1.2. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.WebSocketSubprotocols.UnitLibrary` and `Curl.Extension.WebSocketSubprotocols.UnitTests` exist in `/Extensions/WebSocketSubprotocols/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option naming a WebSocket subprotocol and framing messages for it.
- [x] Unit tests check its wire behaviour against named sections of graphql-transport-ws protocol, WAMP basic profile, STOMP 1.2, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 1, first pass, no code written): requeued for BL-4253. The extension is a transfer hook like
  `--jsonrpc` (`Curl.Extension.JsonRpc.UnitLibrary`): for a ws/wss URL it adds `Sec-WebSocket-Protocol` through
  `RequestHeadersFor` (the ws handler reads `context.Http.Headers`) and replaces the upload with the subprotocol's
  opening messages (graphql-transport-ws `connection_init` + `subscribe`; WAMP `HELLO`; STOMP `CONNECT` +
  `SUBSCRIBE`/`SEND`). But `WsProtocolHandler.SendUploadAsync` frames any plain upload into binary frames of up to
  64 KiB, so several messages would go out as one binary frame, and all three protocols need one text frame per
  message. The per-frame seam (`IWsUploadFrameSource`) is in `Curl.Protocol.Ws`, which an extension may not
  reference. BL-4253 adds `IWsUploadMessageSource` in the shared layer `Curl.WebSocket.UnitLibrary`; this
  extension then references `Curl.WebSocket.UnitLibrary` (allowed: not `Curl.Protocol.*`) and gives its upload as
  one. No other hook is missing: `ExtensionOption`, `ExtensionDescription`, `ITransferHook.ExecuteAsync` and the
  option, description and transfer hook catalogues all exist. The handler sends the whole upload, then receives
  until the server closes, so the messages are pipelined (no wait for `connection_ack`/`WELCOME`/`CONNECTED`);
  record that in the ADR.
- Touches widened to the option, description and transfer hook catalogues: they are shared list files
  (`Tasks/SharedListFiles.txt`), so they make no overlap; `ExtensionCatalogue.cs` (protocol handlers) is not the
  registration a transfer hook extension uses.
- 2026-10-29 (lane 1, resumed): the first criterion's "registered by one line in `ExtensionCatalogue.cs`" is met
  by the transfer hook extension's registration instead: one line each in the option, description and transfer
  hook catalogues, as `--jsonrpc` does (ADR-1028); `--ws-subprotocol` adds no URL scheme, so
  `ExtensionCatalogue.cs` (protocol handler factories) has nothing to register (ADR-1082). The build's isolation
  check passes.
- Quality: `Measure-CodeQuality.ps1` for `Curl.Extension.WebSocketSubprotocols.UnitLibrary` (100% line and branch,
  highest complexity 10, 0 failing) and `Curl.Extension.Composition.UnitLibrary` (100/100, 0 failing).
- The tests use no file system path, OS error or certificate, so they are platform-neutral; fast tests green on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 971c9652 1f2ca384 653e3607 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4253: an upload seam in Curl.WebSocket.UnitLibrary that sends one text frame per message, which graphql-transport-ws, WAMP and STOMP need
- 2026-10-29: Backlog -> Doing. Criteria at claim: 971c9652 1f2ca384 653e3607 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --ws-subprotocol speaks graphql-transport-ws, WAMP and STOMP 1.2 over WebSocket; gates and fast tests green
