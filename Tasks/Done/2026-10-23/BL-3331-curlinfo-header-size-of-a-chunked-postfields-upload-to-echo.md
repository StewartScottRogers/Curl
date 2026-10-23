---
id: BL-3331
title: CURLINFO_HEADER_SIZE of a chunked POSTFIELDS upload to /echo counts as libcurl does (post-chunked-header)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ChunkedUploadSize.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3331 — CURLINFO_HEADER_SIZE of a chunked POSTFIELDS upload to /echo counts as libcurl does (post-chunked-header)

## Goal

Curl's Libcurl answers CURLINFO_HEADER_SIZE (0x20000C) as libcurl 8.22.0 does in the easytest.py case post-chunked-header.

## Context

Found by BL-3320's run of LibcurlEasyDifferentialTests (CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git ucrt64 libcurl-4.dll). post-chunked-header (Transfer-Encoding: chunked header, POSTFIELDS "x", GET of /echo): 0x20000C is 147 in libcurl and 137 in Curl, with equal wire bytes and the same 200 response. BL-3320 fixed 0x600007 (upload size counts chunk framing) but could not explain these 10 bytes; find what libcurl counts as header bytes here (check for a 100-continue exchange or trailer bytes).

## Acceptance criteria

- [x] post-chunked-header is identical in LibcurlEasyDifferentialTests report for 0x20000C, and a focused unit test pins the answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- 2026-10-23: 0x20000C is CURLINFO_REQUEST_SIZE (header size is 0x20000B). The 10 bytes are the chunk framing of the 1-byte body ("1
", "
", "0

"): libcurl counts bytes sent, framing included. Fix: HttpProtocolHandler Report.RequestSize adds upload.BytesSent instead of BytesWritten; test HttpProtocolHandlerTests.ChunkedUploadSize.cs. Differential report no longer lists 0x20000C for post-chunked-header (0x600007 remains, BL-3320). Http tests green (2259); Measure-CodeQuality Curl.Protocol.Http.UnitLibrary: 0 failing members. Platform-neutral, so the CI criterion is met.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 37e91894 db93e725.
- 2026-10-23: Doing -> Done. Request size counts chunk framing as libcurl does
