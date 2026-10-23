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
completed: 2026-10-22
---
# BL-3214 — Frame a chunked MIME upload in the chunks libcurl 8.22.0 sends, with no empty chunk before a part

## Goal

A chunked `CURLOPT_MIMEPOST` upload sends libcurl 8.22.0's chunk framing, one chunk per read upstream makes and no empty chunk before a part's data (BL-3210's case 4).

## Context

Split from BL-3208. Decided by Claude under Stewart's delegation (BL-3208): match upstream's chunking too, since the wire bytes are what a drop-in replacement must match. Measured 2026-10-22 with the curl 8.22.0 tool (`-H "Transfer-Encoding: chunked" -F a=b`): the whole 0x95-byte body goes in one chunk, then `0\r\n\r\n`. We send an empty chunk plus a data chunk for a part; payload bytes are equal. Each read of the body becomes one chunk in `HttpRequestBodyWriter`, so look at how `ConcatenatedReadStream` returns short reads at segment boundaries and fill each read across segments as libcurl's `mime_read` does.

## Acceptance criteria

- [x] `Curl.Core.UnitTests/Multipart/ConcatenatedReadStreamChunkReadTests.cs` pins that a read spans segment boundaries and never returns an empty read before the end.
- [x] `Curl.Protocol.Http.UnitTests/HttpRequestBodyWriterMimeChunkTests.cs` pins the chunk framing BL-3210 recorded for case 4.
- [x] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members for the changed libraries.

## Notes

Decided by Claude under Stewarts delegation: ConcatenatedReadStream now fills every read across segments (as libcurl mime_read does), not only bodies with callback or file segments; a segment failing after bytes of the same fill defers the failure to the next read so bytes before a refused byte still go out (7bit tests). Writer needed no change: it already makes one chunk per read. Existing tests expecting one-segment reads updated. Measure-CodeQuality Curl.Core.UnitLibrary: 0 failing. CI platform criteria met by platform-neutral tests.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 65404b0d ddbbe993 491059de.
- 2026-10-22: Doing -> Done. Chunked MIME upload bodies now fill each read across segments, so no empty chunk precedes a part
