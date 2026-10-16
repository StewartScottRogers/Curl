---
id: BL-2443
title: Engine: IEasyWebSocket receive, send and metadata
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408, BL-2423, BL-2471]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2443 — Engine: IEasyWebSocket receive, send and metadata

## Goal

The managed engine's `IEasyWebSocket` implements `Recv`, `Send` and `Meta` for a `CONNECT_ONLY=2` transfer over `Curl.Protocol.Ws`, with `CURLWS_TEXT/BINARY/CONT/CLOSE/PING/PONG` and auto-pong.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests exchange frames with a fake server connection, including fragmentation and ping.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- 2026-10-16 (lane 2, first pass, no code written): two prerequisites found, so requeued once for both.
  - BL-2423: the managed engine has no connect-only perform yet (`ManagedEasyTransfer.Connection` and `.WebSocket` both throw `NotSupportedException`); a `CONNECT_ONLY` perform that keeps the connection open is BL-2423's engine work, and `CONNECT_ONLY=2` builds on it.
  - BL-2471 (filed): `Curl.Protocol.Ws` keeps its frame codec `internal`, its encoder writes only FIN frames, its decoder gives no per-frame metadata, and `WsProtocolHandler` always reads until the server closes, with no way to stop after the `101` and hand back the connection. The engine cannot reach any of it, and copying the codec into the engine would duplicate it, so the public surface goes into the Ws library first.
  - Once both are Done, this task is the engine side only: honour `ConnectOnly.WebSocket` in perform through BL-2471's upgrade-only entry point, and implement `IEasyWebSocket` (Recv/Send/Meta, `CURLWS_*` flag mapping, auto-pong unless `WebSocketOptionBits.NoAutoPong`) over the held connection with a fake server connection in tests.

- 2026-10-16 (lane 2, second claim): done; ADR-0616 records the design and the libcurl behaviour read from curl 8.22.0's `lib/ws.c` (libcurl cannot be measured from these tests).
  - A `CONNECT_ONLY` 2 perform of a `ws`/`wss` URL runs the normal perform path over a dispatcher of one `WebSocketUpgradeOnlyHandler` (BL-2471's `ConnectAndUpgradeOnlyAsync` over `CreateConnectOnlyConnector`), which hands the connection and the bytes after the `101` to `ManagedEasyWebSocket`. `CONNECT_ONLY` 2 on another scheme only connects.
  - `ManagedEasyWebSocket`: non-blocking receive (`Again`), per-piece offset/bytes-left, continuation fragments carry the message type, auto-pong unless `NoAutoPong`, send opcode chosen as `ws_frame_flags2firstbyte` (`WebSocketFrameFlags`), `CurrentFrame` the last frame received (as `ws->recvframe`).
  - Sensible defaults taken: `CURLWS_OFFSET` and `StartFrameAsync` answer `BadFunctionArgument` for now, filed as BL-2481 (needs a partial-frame encoder in `Curl.Protocol.Ws`, outside this task's touches); with options not connect-only both calls answer `UnsupportedProtocol`, with no upgraded connection `BadFunctionArgument`.
  - The old test `WebSocket_NotYetBuilt_ThrowsNotSupported` pinned the placeholder this task replaces; it became `WebSocket_BeforeAnyPerform_AnswersUnsupportedProtocolWithNoFrame`.
  - Measured: first quality run failed `WebSocketFrameFlags.ControlOpcodeOf` (branch, Cx 12) and `ManagedEasyWebSocket.ReadMoreAsync` (Cx 12); split both, second run 0 failing members. Fast tests all green; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Backlog. Waits on BL-2423 (engine connect-only perform) and BL-2471 (public Ws frame codec and upgrade-only connect)
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. IEasyWebSocket receives, sends and reports frames over a CONNECT_ONLY 2 WebSocket upgrade, with fragmentation and auto-pong
