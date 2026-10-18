# ADR-0678 — The ratchet stands in for the debug build's `CURL_ENTROPY` in the WebSocket libtest cases

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2688.

## Context

Upstream cases 2301 to 2304 run the WebSocket libtest programs (`lib2301`, `lib2302`, `lib2304`), name the `Debug`
feature and set `CURL_ENTROPY=12345678`. In upstream's debug build that variable replaces libcurl's random source
(`lib/rand.c`, `randit`): the first value is the variable's first four bytes read big-endian (`ntohl`), each later
value is one more, the sequence is one static for the whole process, and `Curl_rand_bytes` copies each value's
four bytes in the machine's (little-endian) order. So the `Sec-WebSocket-Key` is the base64 of `4321532163217321`
(`NDMyMTUzMjE2MzIxNzMyMQ==`) and the first frame mask after it is `8321`. Curl has no debug build, so the ratchet
skipped all four as `Curl lacks the feature Debug`.

## Decision

- As for `CURL_FORCETIME` (ADR-0671), the harness stands in for the debug build: `UpstreamConformanceTests.PlatformFor`
  reports `Debug` to a case that names the `ws` feature and sets `CURL_ENTROPY`, and `CreateEngine` gives the libtest
  engine a `ForcedEntropyWebSocketRandomSource` over the variable's value.
- `ManagedCurlEngine`'s internal constructor takes an optional `IWebSocketRandomSource`, kept on
  `ManagedEngineSharedState.WebSocketRandomSource`: one source for the engine, as the debug build's sequence is one
  for the process. `CreateDispatcher` hands it to the WebSocket handler of an ordinary perform, and each
  `ManagedEasyTransfer` starts with it for its connect-only upgrade and its `curl_ws_send` masks. Without it nothing
  changes: `SystemWebSocketRandomSource`.
- The stand-in is the harness's alone. Neither `Libcurl` nor the curl tool reads `CURL_ENTROPY`, as neither is a
  debug build.

## Consequences

- Cases 2301 to 2304 run rather than skip. Case 2303 passes and is on the ratchet; 2301, 2302 and 2304 differ
  after the handshake and are filed as their own tasks.
- Any later case that needs `CURL_ENTROPY` for something other than the WebSocket key and masks needs the stand-in
  extended to that consumer.
