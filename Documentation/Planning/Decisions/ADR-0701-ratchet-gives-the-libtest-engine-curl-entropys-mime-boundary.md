# ADR-0701 — The ratchet gives the libtest engine the MIME boundary the debug build makes under `CURL_ENTROPY`

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2695.

## Context

Upstream case 1972 (`lib1972`, an AWS SigV4 signed `CURLOPT_MIMEPOST`) sets `CURL_ENTROPY=12345678` and expects the
boundary `------------------------qrstuvwxyz0123456789AB`. In upstream's debug build `CURL_ENTROPY` replaces
lib/rand.c's random source with a sequence (the variable's first four bytes read big-endian, each later value one
more), and `curl_mime_init` draws its 22 boundary characters through `Curl_rand_alnum`: each the alphanumeric at the
value's remainder by 62, a value at or past the largest multiple of 62 drawn again. `12345678` starts at `0x31323334`,
whose remainder is 42, `q`. Curl has no debug build; its engine's boundary was always random, so the case differed at
its first boundary line.

## Decision

- `ManagedCurlEngine`'s internal constructor takes `createFormBoundary` (kept on `ManagedEngineSharedState`, copied by a
  share handle's state), which every handle's `ManagedEasyTransfer.CreateFormBoundary` starts from; `null`, as the public
  constructor leaves it, keeps curl's random boundary.
- `UpstreamConformanceTests.CreateEngine` passes `ForcedEntropyFormBoundary.Of(entropy)` when a case sets `CURL_ENTROPY`,
  as ADR-0678 passes the WebSocket key and masks from the same sequence.
- Each boundary starts the sequence from its first value. Upstream's sequence is one per process, so a later random
  draw would continue it; lib1972 draws nothing before its one boundary, and the WebSocket cases draw no boundary, so
  the two stand-ins do not need to share one counter. A case needing that would change it.
- The curl tool's `-F` cases keep their random boundary: upstream's `-F` cases cut it with `<strippart>`.

## Consequences

- test1972 passes and is on the ratchet; its bytes go only to the in-memory `sws`, so nothing depends on the platform.
- Neither `Libcurl` nor the curl tool reads `CURL_ENTROPY`.
