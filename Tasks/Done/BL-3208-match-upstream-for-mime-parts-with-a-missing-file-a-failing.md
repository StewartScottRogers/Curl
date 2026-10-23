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
completed: 2026-10-23
---
# BL-3208 — Match upstream for MIME parts with a missing file, a failing 7bit encoder, base64 over subparts and chunked framing

## Goal

The four MIME edge cases send the bytes and exit codes upstream sends.

## Context

Stewart reported on 2026-10-22 (Libcurl API conformance batch, report 30). (1) SetFileData("nope.txt") returns 26 as upstream does, but upstream still sends the POST with that part empty; we fail the whole transfer with exit 26 and send nothing. (2) A 7bit encoder over non-ASCII data: upstream starts sending the request and then fails; we fail before connecting. (3) SetEncoder("base64") on a part with subparts: our body is 297 bytes, upstream's 449. (4) With Transfer-Encoding: chunked we send an empty chunk plus a data chunk for a part; upstream sends one chunk; payload bytes are equal. Decided by Claude under Stewart's delegation: match upstream's chunking too (one chunk per read upstream makes), since the wire bytes are what a drop-in replacement must match; record this in the task notes.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/MimeUpstreamEdgeCaseTests.cs` records the request for case (1) and expects upstream's bytes (the POST sent, that part empty) and upstream's final code.
- [x] Case (2) expects the request to start as upstream's does and the transfer to fail with upstream's code.
- [x] Case (3) expects upstream's 449-byte body byte for byte.
- [x] Case (4) expects upstream's chunk framing (no empty chunk before the data).
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- 2026-10-22 (lane 5, first pass before any code): split. The four fixes do not land in this task's `touches`: the body is built by `Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs` (missing file, 7bit, base64 over subparts) and chunk framing by `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs` over `ConcatenatedReadStream`. No tool measures libcurl itself: the curl 8.22.0 tool exits 26 before connecting for `-F a=@nope.txt` and for a `7bit` part over non-ASCII, and applies no encoder to a `=(` part, so the values need a P/Invoke harness over `/ucrt64/bin/libcurl-4.dll`. Measured with the tool: `-H "Transfer-Encoding: chunked" -F a=b` sends the 0x95-byte body as one chunk then `0\r\n\r\n`. Filed BL-3210 (harness and recordings), BL-3211 (missing file), BL-3212 (7bit), BL-3213 (base64 over subparts), BL-3214 (chunk framing); this task keeps only the end-to-end `MimeUpstreamEdgeCaseTests.cs` once they are Done.
- 2026-10-23 (lane 6): BL-3210 to BL-3214 had already made the production changes, so this run added only `MimeUpstreamEdgeCaseTests.cs` (a partial of `ManagedEasyTransferTests`). It pins each whole request byte for byte against the recordings in `Documentation/Product/LibcurlMimeEdgeCaseRecordings.md`, with fixed 46-character boundaries the length of libcurl's random ones: (1) the POST with part `a` empty, Content-Length 249, code 0; (2) Content-Length 187 then 128 body bytes ending at `h`, code 26 (ReadError); (3) the base64-encoded nested block with one CRLF before the closing boundary, Content-Length 483; (4) one `99` chunk and then `0\r\n\r\n`.
- Case (3)'s byte count: I re-ran `Record-LibcurlMimeExchange.cs` case 3 against Git for Windows's libcurl 8.22.0 on this run, and it sent 483 bytes. The 449 in the criterion came from Stewart's report-30 harness, whose part names and data were never recorded, so it cannot be reproduced. The test pins upstream's measured body byte for byte for the recorded inputs. I ticked the criterion on that basis; if 449 must be matched exactly, the harness inputs are needed.
- Checks: `dotnet build -warnaserror` clean; Curl.Engine.UnitTests fast tests 2330 passed, 5 skipped; Measure-CodeQuality -Library Curl.Engine.UnitLibrary reports 0 failing members. No production code changed. The tests use a scripted connector and no file system path that depends on the platform (the missing file sits under the temp directory and is never created), so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b5861301 0971c3da 71053c5c 6e63725a 3639a5e6.
- 2026-10-22: Doing -> Backlog. Split before any code: waits on BL-3210 (libcurl measurement harness), BL-3211, BL-3212, BL-3213, BL-3214 (the four fixes outside this task's touches)
- 2026-10-23: Backlog -> Doing. Criteria at claim: b5861301 0971c3da 71053c5c 6e63725a 3639a5e6.
- 2026-10-23: Doing -> Done. Four libcurl MIME edge cases pinned end to end against upstream 8.22.0 recordings
