# ADR-1017 — The WebSocket frame codec is a shared layer, and its decoder reports to an observer

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4196.

## Context

WebSocket over HTTP/2 and HTTP/3 (BL-4112), `--ws-repl` (BL-4111) and WebSocket subprotocols
(BL-4114) are extensions, and an extension references no `Curl.Protocol.*` library but
`Curl.Protocol.Abstractions` (ADR-0999). All three need the RFC 6455 frame codec, which lived in
`Curl.Original.Protocol.Ws.UnitLibrary`. Its decoder wrote the `ws` diagnostic log (`WsTransferLog`) and the
`--trace-config ws` lines (`WsFrameTrace`) itself, and failed with `WsTransferException` carrying
exit 56, all types tied to `Curl.Protocol.Abstractions`.

## Decision

- `Curl.WebSocket.UnitLibrary` (namespace `Curl.WebSocket`, solution folder `/Wire/WebSocket/`)
  holds `WsOpcode`, `WsFrameEncoder`, `IWebSocketRandomSource`, `SystemWebSocketRandomSource`,
  `WsFrameDecoder`, `WsDecodedBytes`, `WsFrameRun`, `WsDecodedFrames` and
  `WsFrameMetadataDecoder`. It references no project at all, so any layer may use it. It joins
  ADR-0120's table of hand-built libraries a protocol library may reference, with nothing below it.
- `WsFrameDecoder` tells an `IWsFrameDecodeObserver` what it decodes (frame opened, head ending a
  read, payload passed, close payload). `Curl.Original.Protocol.Ws`'s `WsFrameDecodeReporter` turns that into
  exactly the log and trace lines the decoder wrote before.
- A violation comes back as `WsDecodedBytes.ViolationMessage`, curl 8.21.0's `[WS]` text;
  `WsFrameReceiver` turns it into the same `WsTransferException` (exit 56, `IsFrameViolation`) as
  before. curl's behaviour does not change.
- The projects that used the moved types (`Curl.Original.Protocol.Ws` and its tests, `Curl.Engine` and its
  tests, `Curl.Console`, `Curl.Conformance.UnitTests`) import `Curl.WebSocket` with one `<Using>` in
  their project file, rather than a `using` line in each of some forty files, several of them
  held by other tasks at the time.
- The test fakes `FixedRandomSource` and `ScriptedRandomSource` live in `Curl.WebSocket.UnitTests`
  and are linked into `Curl.Original.Protocol.Ws.UnitTests`.

## Consequences

- Extensions can encode and decode WebSocket frames without a protocol library.
- The decoder's own unit tests no longer pin exit 56; `WsFrameReceiver`'s tests still do.

## Alternatives considered

- **Keep the namespace `Curl.Original.Protocol.Ws` in the new library.** Breaks the rule that a namespace
  matches its folder.
- **Let the shared layer reference `Curl.Protocol.Abstractions`** for `IDiagnosticLog` and
  `CurlExitCode`. BL-4196 asks for a layer that references no `Curl.Protocol.*` library, and the
  decoder needs neither once an observer carries what it decodes.
