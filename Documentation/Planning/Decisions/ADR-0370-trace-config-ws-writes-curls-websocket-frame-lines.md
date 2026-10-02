# ADR-0370 — `--trace-config ws` writes curl's WebSocket frame lines

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1164, split from BL-1104 (ADR-0318), following BL-1162's and BL-1163's pattern. curl 8.21.0
(Schannel) writes `* [WS] ...` lines under `-v --trace-config ws` (and `protocol`, `all`, so
`-vv`). Measured with `Record-CurlExchange.ps1` against a `101` head followed in the same read
by: a text frame and an empty close; a binary frame and a close with a code; a non-final text
fragment and its continuation; two pings; an empty ping and a text frame; nothing; a text frame
with a `-T` upload; and a text frame under `-I`. A frame split across reads cannot be measured
with the recorder, which sends each response in one write.

## Decision

1. `WsFrameTrace` writes the lines through `ITransferEvents.ReportInfo`; the handler's
   `TracesFrames` turns it on, and `CurlComposition.TracesWs` sets it from the trace components.
   The decoder writes each frame's lines as it decodes, the receiver the pong lines, the handler
   the rest.
2. Frames are named as curl's trace names them: `CONT`, `TEXT`, `BIN`, `CLOSE`, `PING`, `PONG`,
   with ` NON-FINAL` when FIN is clear. A zero-length frame writes only its `decoded decoded` line.
3. `websocket established, callback mode` follows the bytes that came with the `101`, decoded
   or (under `-I`) not, and comes before the upload frame.
4. Unmeasured, from curl's `ws_dec_pass`: a frame whose head ends a read writes
   `decoded passing [... payload=0/<len>]` straight away, as the decoder falls through from the
   head to an empty payload pass; each later read writes its `passed` and `passing` lines.
5. curl's `abort upload`, written only when the server closes before the upload frame goes, is
   not written: Curl always sends the upload frame (ADR-0131), so the line would be false.

## Consequences

The measured exchange's stderr matches real curl's from the `101` on. Without the component
nothing changes. A frame violation writes no trace line before its message, as the head never
completes.

## Alternatives considered

- Passing `ITransferEvents` and a flag to every Ws class instead of one trace object: more
  constructor churn for the same lines.
