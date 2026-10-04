---
id: BL-1431
title: Fail a transfer past 5000 response headers with exit 100 and warn about HTTP/2 request headers past 60000 bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1430]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-067
created: 2026-10-04
completed:
---
# BL-1431 — Fail a transfer past 5000 response headers with exit 100 and warn about HTTP/2 request headers past 60000 bytes

## Goal

A response that carries more than 5000 header lines fails with curl 8.21.0's `curl: (100) Too many response headers, 5000 is max`, and an HTTP/2 request whose header names and values add up to more than 60000 bytes writes curl's `[HTTP/2] Warning: The cumulative length of all headers exceeds 60000 bytes and that could cause the stream to be rejected.` `-v` line.

## Context

- Upstream (tag `curl-8_21_0`): `lib/headers.c` lines 253-257 (`Curl_headers_push`) refuse the next header once `MAX_HTTP_RESP_HEADER_COUNT` (5000, `lib/headers.h` line 163) are stored, with `failf("Too many response headers, %d is max")` and `CURLE_TOO_LARGE` (exit 100). Headers are pushed by the `hds-collect` client writer (`lib/headers.c` lines 296-312) for every header line - not status lines, not the empty line - of every head of the transfer: 1xx heads, CONNECT heads, trailers, and the hops of a followed redirect, since the list is cleared only by `Curl_headers_cleanup` in `Curl_pretransfer` (`lib/transfer.c` line 601). A continuation line folds into the header before it.
- Upstream test 747 ("Reject too many HTTP response headers", vendored as `Curl.Conformance.UnitTests/UpstreamTestData/test747.rawhttp`) expects exit 100; Curl exits 0 today (the conformance run reports `test747 fails: <verify><errorcode>: expected exit code 100, got 0`).
- Upstream `lib/http2.c` lines 2145-2165: under `-v`, after the `[HTTP/2] [<id>] [name: value]` lines of a new stream, if the sum of every header's name length plus value length (in bytes, pseudo-headers included) exceeds `MAX_ACC` 60000, curl writes `[HTTP/2] Warning: The cumulative length of all headers exceeds 60000 bytes and that could cause the stream to be rejected.` (no stream id in this line). HTTP/3 (`lib/vquic/curl_ngtcp2.c`) has no such warning.
- Curl today: `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` has no header count limit; `HttpStreamOpenedLines.Report` writes the OPENED and header lines but no warning. Depends on BL-1430 only because both change `Curl.Protocol.Http.UnitLibrary`.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Http.UnitTests` pins that a final head of 5000 header lines succeeds and one of 5001 fails with exit 100 and the message `Too many response headers, 5000 is max`; another pins that the count spans the heads of one transfer (a 100 Continue head's headers plus the final head's), and a third that a continuation line does not count as a header of its own.
- [ ] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"` reports `test747 passes; add 747 to PassingUpstreamCases.txt` (listing it is left to the conformance tasks, so this task does not touch that project), or the reason it still differs is written in Notes.
- [ ] A test pins that an HTTP/2 request whose header names and values total 60001 bytes writes the warning line, byte for byte, right after its last `[HTTP/2] [1] [name: value]` line, and one totalling exactly 60000 bytes does not; no HTTP/3 stream ever writes it.
- [ ] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
