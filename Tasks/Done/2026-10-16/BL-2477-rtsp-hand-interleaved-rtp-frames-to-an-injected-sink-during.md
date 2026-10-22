---
id: BL-2477
title: RTSP: hand interleaved $ RTP frames to an injected sink during RECEIVE
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2477 — RTSP: hand interleaved $ RTP frames to an injected sink during RECEIVE

## Goal

During an RTSP `RECEIVE` (and any reply with interleaved data), `Curl.Protocol.Rtsp.UnitLibrary` splits each interleaved `$`-framed RTP packet out of the stream and hands the whole packet, header included, to an injected sink, whose default writes it to the body output as curl does.

## Context

Prerequisite of BL-2420 (CURLOPT_INTERLEAVEFUNCTION in the engine). Today `RtspReplyReader` only skips bytes before `RTSP/` (BL-1663); nothing hands RTP frames anywhere (BL-2420 lane, 2026-10-16). Follow upstream curl 8.22.0's `lib/rtsp.c` (`rtsp_filter_rtp`, `rtp_client_write`): a frame is `$`, channel byte, 16-bit big-endian length, payload; a sink taking fewer bytes than the frame fails the transfer with `CURLE_WRITE_ERROR` (23); a frame split across reads is joined before the call. Measure real curl with `Record-CurlExchange.ps1` for the default (no callback) output bytes. The sink interface lives in the RTSP library or `Curl.Protocol.Abstractions.UnitLibrary` (if the latter, add it to touches); the engine adapts `InterleaveCallback` to it in BL-2420.

## Acceptance criteria

- [x] Unit tests pin a RECEIVE with two frames, a frame split across reads, frames between RTSP replies, and a sink that takes too few bytes (exit 23).
- [x] Without a sink set, the output bytes match real curl's for the same exchange (measured, recorded under Notes).
- [x] `dotnet build` is clean and the fast tests pass; `PassingUpstreamCases.txt` unchanged or grown.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 0 failing members.

## Notes

- Design (ADR-0612): `IRtspInterleaveSink` (`ValueTask<long> WriteAsync(frame, token)`; the answer is the bytes taken, so BL-2420 passes `InterleaveCallback`'s answer through) lives in the RTSP library; `Curl.Protocol.Abstractions` is unchanged. It is an optional third constructor argument of `RtspProtocolHandler`. `RtspInterleavedFrameFilter` hands every whole frame on every channel (the library sends no SETUP, so the sink filters by the channel byte) and leaves a partial one in the buffer to join with the next read; a short take fails with 23, `Failed writing RTP data`.
- RECEIVE is `RtspProtocolHandler.ReceiveAsync` (internal): sends nothing, checks no CSeq, reads until the server closes; with no sink it writes frames to the body output (`RtspStreamInterleaveSink`). BL-2420 still needs the engine to reach it once `CURLOPT_RTSP_REQUEST` is carried to the handler.
- Measured curl 8.22.0 with Record-CurlExchange.ps1: `curl -s -i rtsp://127.0.0.1:47791/` against two frames, then `RTSP/1.0 200 OK` / `CSeq: 1` / `Content-Length: 2` / body `hi`, then a trailing frame: exit 0, stdout is the head only. With no sink the handler does the same (`ExecuteAsync_FramesBeforeTheReplyWithNoSink_SkipsThemAsCurlDoes`).
- Default taken: a reply that is only a partial frame before the close fails with 52; the 52 check now asks whether the unread bytes start with `RTSP/`, which is the same answer as before on the no-sink path.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10. RTSP tests: 300 passed, 6 skipped (platform). Full fast run: no failures. PassingUpstreamCases.txt untouched.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. RTSP hands each interleaved dollar RTP frame whole to an injected sink before a reply and during RECEIVE; a short take fails with 23
