---
id: BL-3208
title: Match upstream for MIME parts with a missing file, a failing 7bit encoder, base64 over subparts and chunked framing
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3210, BL-3211, BL-3212, BL-3213, BL-3214]
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

- 2026-10-22 (lane 5, first pass before any code): split. The four fixes do not land in this task's `touches`: the body is built by `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs` (missing file, 7bit, base64 over subparts) and chunk framing by `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs` over `ConcatenatedReadStream`. No tool measures libcurl itself: the curl 8.22.0 tool exits 26 before connecting for `-F a=@nope.txt` and for a `7bit` part over non-ASCII, and applies no encoder to a `=(` part, so the values need a P/Invoke harness over `/ucrt64/bin/libcurl-4.dll`. Measured with the tool: `-H "Transfer-Encoding: chunked" -F a=b` sends the 0x95-byte body as one chunk then `0\r\n\r\n`. Filed BL-3210 (harness and recordings), BL-3211 (missing file), BL-3212 (7bit), BL-3213 (base64 over subparts), BL-3214 (chunk framing); this task keeps only the end-to-end `MimeUpstreamEdgeCaseTests.cs` once they are Done.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b5861301 0971c3da 71053c5c 6e63725a 3639a5e6.
- 2026-10-22: Doing -> Backlog. Split before any code: waits on BL-3210 (libcurl measurement harness), BL-3211, BL-3212, BL-3213, BL-3214 (the four fixes outside this task's touches)
- 2026-10-23: Backlog -> Doing. Criteria at claim: b5861301 0971c3da 71053c5c 6e63725a 3639a5e6.
