# ADR-0612 — RTSP hands interleaved `$` RTP frames whole to an injected sink

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2477.

## Context

libcurl's `CURLOPT_INTERLEAVEFUNCTION` (`InterleaveCallback` in `Curl.Api`) takes each interleaved RTP frame an
RTSP transfer reads. In curl 8.22.0 `rtsp_filter_rtp` splits each frame - `$`, a channel byte, a 16-bit
big-endian payload length and the payload - out of the bytes read outside a reply, joins one split across reads,
and `rtp_client_write` hands it whole, header included, to the interleave function, or to the write function
when none is set; a function taking fewer bytes than the frame fails with `CURLE_WRITE_ERROR` (23),
`Failed writing RTP data`. Only channels set up by a `SETUP` reply's `Transport: ...;interleaved=a-b` count;
other frames are skipped. An `RTSPREQ_RECEIVE` sends nothing and reads frames until the server closes, with no
`CSeq` check. `Curl.Original.Protocol.Rtsp.UnitLibrary` made only the curl tool's `OPTIONS` request and skipped every
frame (BL-1663).

Measured on curl 8.22.0 with `Record-CurlExchange.ps1` (BL-2477): `curl -s -i rtsp://127.0.0.1:47791/` against
`$\0\0\4abcd$\1\0\2xy` + `RTSP/1.0 200 OK`, `CSeq: 1`, `Content-Length: 2`, body `hi`, then `$\0\0\3zzz` exits 0
and writes only the head to stdout: the tool sets up no channel, so its frames never reach the output.

## Decision

1. **The sink is `IRtspInterleaveSink`, in the RTSP library.** `ValueTask<long> WriteAsync(frame, token)`: the
   frame whole, header included; the answer is the bytes taken, so the engine's adapter (BL-2420) passes
   `InterleaveCallback`'s answer through. `Curl.Protocol.Abstractions` stays unchanged: only RTSP has the concept.
2. **Injected through `RtspProtocolHandler`'s constructor**, optional. With none - the curl tool, which sets up no
   channel - frames before a reply are skipped unwritten, as measured, and a `RECEIVE` writes its frames to the
   body output through `RtspStreamInterleaveSink`, curl's fall-back to the write function.
3. **Every channel goes to the sink.** The library sends no `SETUP` of its own, so it keeps no channel mask; the
   sink sees the channel byte and keeps the channels its caller set up. Bytes outside a frame are skipped, as
   curl skips junk.
4. **`RECEIVE` is `RtspProtocolHandler.ReceiveAsync`:** sends nothing, reads until the server closes, checks no
   `CSeq`; a frame cut off by the close is dropped. The engine calls it once `CURLOPT_RTSP_REQUEST` reaches the
   handler.
5. **A sink taking a different count fails with 23**, `Failed writing RTP data`.

## Consequences

Frames between replies of one transfer reach the sink when they arrive after a reply's last byte has been read;
bytes after a reply's body in the same read are still dropped with the rest of that read, as before.
