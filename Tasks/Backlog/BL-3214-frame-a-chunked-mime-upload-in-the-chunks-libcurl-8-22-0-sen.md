---
id: BL-3214
title: Frame a chunked MIME upload in the chunks libcurl 8.22.0 sends, with no empty chunk before a part
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-3210]
touches: [Curl.Core.UnitLibrary/Multipart/ConcatenatedReadStream.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Core.UnitTests/Multipart/ConcatenatedReadStreamChunkReadTests.cs, Curl.Protocol.Http.UnitTests/HttpRequestBodyWriterMimeChunkTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3214 — Frame a chunked MIME upload in the chunks libcurl 8.22.0 sends, with no empty chunk before a part

## Goal

A chunked `CURLOPT_MIMEPOST` upload sends libcurl 8.22.0's chunk framing, one chunk per read upstream makes and no empty chunk before a part's data (BL-3210's case 4).

## Context

Split from BL-3208. Decided by Claude under Stewart's delegation (BL-3208): match upstream's chunking too, since the wire bytes are what a drop-in replacement must match. Measured 2026-10-22 with the curl 8.22.0 tool (`-H "Transfer-Encoding: chunked" -F a=b`): the whole 0x95-byte body goes in one chunk, then `0\r\n\r\n`. We send an empty chunk plus a data chunk for a part; payload bytes are equal. Each read of the body becomes one chunk in `HttpRequestBodyWriter`, so look at how `ConcatenatedReadStream` returns short reads at segment boundaries and fill each read across segments as libcurl's `mime_read` does.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests/Multipart/ConcatenatedReadStreamChunkReadTests.cs` pins that a read spans segment boundaries and never returns an empty read before the end.
- [ ] `Curl.Protocol.Http.UnitTests/HttpRequestBodyWriterMimeChunkTests.cs` pins the chunk framing BL-3210 recorded for case 4.
- [ ] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-22: Created.
