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
completed:
---
# BL-2477 — RTSP: hand interleaved $ RTP frames to an injected sink during RECEIVE

## Goal

During an RTSP `RECEIVE` (and any reply with interleaved data), `Curl.Protocol.Rtsp.UnitLibrary` splits each interleaved `$`-framed RTP packet out of the stream and hands the whole packet, header included, to an injected sink, whose default writes it to the body output as curl does.

## Context

Prerequisite of BL-2420 (CURLOPT_INTERLEAVEFUNCTION in the engine). Today `RtspReplyReader` only skips bytes before `RTSP/` (BL-1663); nothing hands RTP frames anywhere (BL-2420 lane, 2026-10-16). Follow upstream curl 8.22.0's `lib/rtsp.c` (`rtsp_filter_rtp`, `rtp_client_write`): a frame is `$`, channel byte, 16-bit big-endian length, payload; a sink taking fewer bytes than the frame fails the transfer with `CURLE_WRITE_ERROR` (23); a frame split across reads is joined before the call. Measure real curl with `Record-CurlExchange.ps1` for the default (no callback) output bytes. The sink interface lives in the RTSP library or `Curl.Protocol.Abstractions.UnitLibrary` (if the latter, add it to touches); the engine adapts `InterleaveCallback` to it in BL-2420.

## Acceptance criteria

- [ ] Unit tests pin a RECEIVE with two frames, a frame split across reads, frames between RTSP replies, and a sink that takes too few bytes (exit 23).
- [ ] Without a sink set, the output bytes match real curl's for the same exchange (measured, recorded under Notes).
- [ ] `dotnet build` is clean and the fast tests pass; `PassingUpstreamCases.txt` unchanged or grown.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
