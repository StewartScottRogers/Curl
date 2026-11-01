# ADR-0698 — The ratchet stands in for the debug build's `CURL_WS_FORCE_ZERO_MASK` in the WebSocket libtest cases

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2698.

## Context

Upstream cases 2700 to 2723 run `lib2700` (ported by BL-2551), name the `Debug` and `ws` features and set
`CURL_WS_FORCE_ZERO_MASK=1`. In upstream's debug build that variable makes `lib/ws.c` mask every frame it sends with
zero, so the case's `<protocol>` holds the exact echo bytes; the `Sec-WebSocket-Key` stays random and the case strips
it. Curl has no debug build, so the ratchet skipped all 24 as `Curl lacks the feature Debug`.

## Decision

- As for `CURL_ENTROPY` (ADR-0678), the harness stands in for the debug build: `UpstreamConformanceTests.PlatformFor`
  reports `Debug` to a case that names the `ws` feature and sets `CURL_WS_FORCE_ZERO_MASK`, and `CreateEngine` gives
  the libtest engine a `ZeroMaskWebSocketRandomSource` through the `IWebSocketRandomSource` seam ADR-0678 added.
- `ZeroMaskWebSocketRandomSource` fills a frame mask (`WsFrameEncoder.MaskLength` bytes) with zeros and anything else,
  the key, from `SystemWebSocketRandomSource`, as the debug build zeroes only the mask. Telling the two by length keeps
  the change inside the harness: no engine or protocol library changes.
- When a case sets both variables, `CURL_ENTROPY` wins; no vendored case does.
- The stand-in is the harness's alone. Neither `Libcurl` nor the curl tool reads `CURL_WS_FORCE_ZERO_MASK`.

## Consequences

- Cases 2700 to 2723 run rather than skip. All but 2708 pass on Windows and are on the ratchet; they exchange bytes
  with the in-memory `sws` only, so nothing in them depends on the platform. 2708 (automatic PONG) differs and is
  BL-2782.
- If a later WebSocket fill of four bytes were not a mask, the stand-in would zero it too; it would then need a
  mask-specific seam in `Curl.Original.Protocol.Ws.UnitLibrary`.
