---
id: BL-3208
title: Match upstream for MIME parts with a missing file, a failing 7bit encoder, base64 over subparts and chunked framing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMimeBuilder.cs, Curl.Engine.UnitLibrary/ManagedMimePartBuilder.cs, Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs, Curl.Engine.UnitTests/MimeUpstreamEdgeCaseTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3208 — Match upstream for MIME parts with a missing file, a failing 7bit encoder, base64 over subparts and chunked framing

## Goal

The four MIME edge cases send the bytes and exit codes upstream sends.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 30). (1) SetFileData("nope.txt") returns 26 as upstream does, but upstream still sends the POST with that part empty; we fail the whole transfer with exit 26 and send nothing. (2) A 7bit encoder over non-ASCII data: upstream starts sending the request and then fails; we fail before connecting. (3) SetEncoder("base64") on a part with subparts: our body is 297 bytes, upstream's 449. (4) With Transfer-Encoding: chunked we send an empty chunk plus a data chunk for a part; upstream sends one chunk; payload bytes are equal. Decided by Claude under Stewart's delegation: match upstream's chunking too (one chunk per read upstream makes), since the wire bytes are what a drop-in replacement must match; record this in the task notes.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/MimeUpstreamEdgeCaseTests.cs` records the request for case (1) and expects upstream's bytes (the POST sent, that part empty) and upstream's final code.
- [ ] Case (2) expects the request to start as upstream's does and the transfer to fail with upstream's code.
- [ ] Case (3) expects upstream's 449-byte body byte for byte.
- [ ] Case (4) expects upstream's chunk framing (no empty chunk before the data).
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
