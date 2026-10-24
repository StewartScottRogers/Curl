---
id: BL-3240
title: Add differential cases for the curl_ws_* WebSocket API
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3237]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/WebSocket, Curl.Conformance.Differential.UnitTests/Libcurl/WebSocket, Curl.Conformance.Differential.IntegrationTests/LibcurlWebSocket]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3240 — Add differential cases for the curl_ws_* WebSocket API

## Goal

New differential cases exercise the `curl_ws_*` WebSocket API (`curl_ws_send`, `curl_ws_recv`, `curl_ws_meta`, connect-only and callback modes, fragments, ping and pong, close) against Curl's Libcurl and libcurl 8.22.0 on a loopback WebSocket server.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester did not cover WebSocket. Measure libcurl 8.22.0 before pinning any value.

## Acceptance criteria

- [x] WebSocket cases covering send, receive, meta, fragments, control frames and close replay against both libraries; one run's report is recorded under Notes, each difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- Plan: `Libcurl/WebSocket/` in the UnitLibrary holds the cases (`LibcurlWebSocketCases.All`, 12: send text/binary/close, receive text/binary/close, received fragments, a 4-byte buffer over a 10-byte frame (offset/bytesleft), sent fragments (CURLWS_CONT), CURLWS_OFFSET with fragsize, an auto-pong to a server ping, a sent ping and pong, a 300-byte extended-length frame, curl_ws_meta outside a callback, recv after the server closes and hangs up, and a callback-mode perform reading curl_ws_meta per write), the per-call line writer and a scripted loopback responder (logs the upgrade without its key and every client frame unmasked; answers ping with pong and close with close). The IntegrationTests `LibcurlWebSocket/` drives both libraries, one loopback connection per case and library, and reuses `LibcurlEasyComparer` and `LibcurlEasyReportWriter`, so the report has the easy_report.txt shape.
- Decided (default): the reference libcurl is loaded in process with NativeLibrary as ADR-0792 does for the easy cases, so no new ADR; a receive answering CURLE_AGAIN is retried every 10 ms up to 200 times before its answer is recorded; the Host line's port is blanked in the wire log because each run listens on its own port.
- Run 2026-10-23 against C:Program FilesGitucrt64binlibcurl-4.dll (8.22.0, Schannel, debug build): `cases 12 identical 11 different 1`. The one difference, ws_meta_outside_callback: after a curl_ws_recv on a connect-only handle libcurl answers curl_ws_meta with NULL, Curl answers the received frame (age=0 flags=1 offset=0 bytesleft=0 len=1) - filed as BL-3312.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members, 100% line and branch, worst CRAP 10 (first run flagged ReadFrameAsync at complexity 16; split). Builds clean with -warnaserror; Curl.Conformance.Differential.UnitTests 504 passed. The new unit tests are in-memory only (no paths, OS text or certificates), so platform-neutral for Linux and macOS CI; the integration test uses only loopback sockets and is inconclusive without the reference library.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: c2a0f6f5 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. Twelve curl_ws_* differential cases replay against Curl's Libcurl and libcurl 8.22.0 on a loopback WebSocket server; the one difference is filed as BL-3312
