---
id: BL-5291
title: Keep an RTSP session and read interleaved data on its connection in CURL_RTSPREQ_RECEIVE
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Libcurl.UnitTests/RtspReceiveSessionTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5291 — Keep an RTSP session and read interleaved data on its connection in CURL_RTSPREQ_RECEIVE

## Goal

In the port's managed `Libcurl`, a `CURL_RTSPREQ_RECEIVE` after `SETUP` and `PLAY` reads the interleaved RTP and RTCP data on the session's existing RTSP connection, one frame per call as upstream libcurl 8.21.0 delivers them, keeps `CURLINFO_RTSP_SESSION_ID` and the CSeq counters, and waits until `CURLOPT_TIMEOUT` (28) when nothing arrives.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 14 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `Libcurl.dll` SHA-256 `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311`), on Windows x64, 2026-11-06, beside upstream libcurl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Libcurl.md`, finding 1.
- **Surl test:** `CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests.PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye` (mirrors `PinnedLibcurlTalksToSurlOverRtspTests` of the same name), Surl commit `2006b0770c8c905bd36f4db799c7fcdd7dd3e0e2`.
- **Script:** `Run-LibcurlRtspScript.cs --timeout 3000 rtsp://{surl}/clip.bin stream-uri:rtsp://{surl}/clip.bin transport:RTP/AVP/TCP;interleaved=0-1 SETUP PLAY RECEIVE RECEIVE RECEIVE` (Surl's twin `Run-CurlPortLibcurlRtspScript.cs` drives the port's `Libcurl` with the same arguments), against `surl rtsp://...` serving a 100-byte `clip.bin`.
- **Upstream libcurl 8.21.0 (correct):** `libcurl-4.dll` SHA-256 `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574`: exit 0. First `RECEIVE`: `session "<session>", cseq received 2, next cseq 3`, one interleave call of 116 bytes (the RTP frame); the second delivers the 40-byte RTCP frame; the third ends `CURLcode 28 (Timeout was reached)`. The server sees one connection.
- **Port (defect):** first `RECEIVE` (report line 5): `session none, cseq received 0, next cseq 0`, two interleave calls (116 and 40 bytes); the second and third return `CURLcode 0` at once with nothing. The server notes Exchanges 2 and 3: the port's `RECEIVE`s open new connections.
- **What decides:** libcurl 8.21.0 `CURLOPT_RTSP_REQUEST` (https://curl.se/libcurl/c/CURLOPT_RTSP_REQUEST.html), `CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CSEQ_RECV`, `CURLINFO_RTSP_CLIENT_CSEQ`; RFC 2326 section 10.12.
- **Where to start:** `Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` (`RtspRequestKind.Receive`) and how `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs` keeps the RTSP connection and session between performs. Earlier work: BL-2367, BL-2477, BL-2704, BL-2787.

## Acceptance criteria

- [ ] After `SETUP` and `PLAY`, `CURL_RTSPREQ_RECEIVE` reads on the same connection (no new connection opened) and keeps `CURLINFO_RTSP_SESSION_ID` and both CSeq counters, shown by a port test.
- [ ] Each `RECEIVE` hands the interleave callback the frames available, and a `RECEIVE` with nothing to read returns `CURLE_OPERATION_TIMEDOUT` (28) after `CURLOPT_TIMEOUT`, shown by a port test.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 02d77192 e574b194.
