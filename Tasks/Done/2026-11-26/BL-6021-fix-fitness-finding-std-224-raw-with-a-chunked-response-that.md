---
id: BL-6021
title: Fix fitness finding STD-224: --raw with a chunked response that has trailers makes %{num_headers} 1
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseBody.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ChunkedTrailerNumHeaders.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6021 — Fix fitness finding STD-224: --raw with a chunked response that has trailers makes %{num_headers} 1

## Goal

STD-224 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: --raw with a chunked response that has trailers makes %{num_headers} 1. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (Windows against go-httpbin and a mock; Linux against go-httpbin) · **Area:** HTTP write-out, both platforms.
- **Server:** `docker run -d --name httpbin -p 127.0.0.1:24280:8080 mccutchen/go-httpbin:2.26.0` (`/trailers?X-T=1` sends a chunked body with a trailer).
- **Repro:** `curl -sS --raw -o /dev/null "http://127.0.0.1:24280/trailers?X-T=1" -w "%{num_headers}\n"`
- **Expected (REF 8.22 and REF2):** `6`.
- **Actual (.NET):** `1`. Without `--raw` both print 1; `size_header` matches.
- **Likely location:** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseBody.cs` / `HttpResponseBodyReader.cs`: the header count is reset for the trailer block even when `--raw` leaves trailers unparsed.
- **Acceptance check:** the repro prints `6`.

## Acceptance criteria

- [x] the repro prints `6`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced and measured with Record-CurlExchange.ps1 against curl 8.21.0 Schannel (Git for Windows). Under `--raw`: a head of 3 headers and 1 trailer prints `num_headers` 3; 5001 trailers exit 0 (no 5000-header limit); a trailer with no colon still exits 8 `Header without colon`. Without `--raw`: num_headers 1, 5001 trailers exit 100.
- Fix: `HttpChunkedDecoder.KeepsTrailers` is false for `--raw`, so trailers are still checked but neither stored, counted nor limited. Matching measured curl, so no ADR.
- The built curl.exe on the finding-shaped response (6 head headers, 1 trailer) prints 6 with `--raw` and 1 without; 5001 trailers with `--raw` match real curl.
- Added HttpChunkedDecoder.cs and the ChunkedTrailerNumHeaders test file to `touches`; no other task in Doing names the HTTP library.
- Fast tests passed; Measure-CodeQuality for the HTTP library: 100% line and branch, 0 failing members.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: c0784398 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. --raw chunked responses with trailers report the head's header count in num_headers, as curl does
