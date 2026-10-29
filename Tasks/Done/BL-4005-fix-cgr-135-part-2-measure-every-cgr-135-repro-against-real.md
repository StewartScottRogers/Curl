---
id: BL-4005
title: Fix CGR-135 part 2: measure every CGR-135 repro against real curl, close the cltwo gap and the quality gates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3667, BL-3668, BL-3979, BL-4004]
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr135Tests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4005 — Fix CGR-135 part 2: measure every CGR-135 repro against real curl, close the cltwo gap and the quality gates

## Goal

Every CGR-135 repro, run through real curl and through Curl, gives the same result, including `num_headers` for the `cltwo` case, and the library stays inside its quality gates.

## Context

Second half of BL-3664 (split); BL-4004 lands the fix first. BL-3664's Notes hold the REF-LINUX measurements and the PORT results of the earlier run: every repro matched except `cltwo` (`[2 200 1 0 32 0]` against REF's `[2 200 1 0 32 1]`), which BL-3979 addressed.

## Acceptance criteria

- [x] Each repro command in CGR-135 (see BL-3664's Notes) is run through real curl and through Curl; the commands and both results are recorded in Notes, all matching (exit code, stdout bytes, stderr text, GOAWAY on the wire), including `cltwo` with num_headers 1.
- [x] If a gap remains, a focused test in `Cgr135Tests.cs` and the fix close it.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` passes (100% line/branch coverage on the new code, complexity, CRAP).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3664 (over its cost cap).
- 2026-10-28 (lane 3): touches widened to `HttpProtocolHandler.cs` (no task in Doing on origin/work/dark-factory names it): the `cltwo` gap was there. BL-3979 had fixed `ResponseHeadersStored`, but write-out's `num_headers` is `ResponseHeaders.Count + PseudoHeaders.Count`, and `WithHeadBeforeFailure` left `ResponseHeaders` empty. It now holds the whole header lines in the failed head's bytes (`HeadersBeforeFailure`), as curl stores each line it writes. Test: `Cgr135Tests.ExecuteAsync_Http2HeadWithContentLengthTwice_ReportsTheHeaderLineWrittenBeforeTheFailure`.
- Measured 2026-10-28, every scenario of BL-3664's Notes plus the two controls, through REF-LINUX (curl 8.18.0, nghttp2 1.68, `wsl.exe -e curl`, `-ListenAddress 172.26.96.1`) and PORT (this checkout, `dotnet run --project Curl.Console --no-build`), each with `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames <frames> -Response 'HTTP/1.1 200 OK\r\n\r\n'` and `-sS --http2-prior-knowledge -i <url> -w '\n[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_header} %{num_headers}]\n'`. Identical exit code, stdout bytes, stderr and GOAWAY on both:
  - upper, connhdr, keepalivehdr, upgradehdr, proxyconn, resptegzip, unkpseudo, dupstatus: 16, `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [<name>], value: [<value>]`, stdout `HTTP/2 200 \r\n` + `\n[2 200 1 0 13 0]\n`, GOAWAY last 0 error 1.
  - cltwo: 16 naming [content-length] [9], stdout `HTTP/2 200 \r\ncontent-length: 9\r\n` + `\n[2 200 1 0 32 1]\n` (PORT gave `... 32 0]` before the fix), GOAWAY 0/1.
  - pseudoafter: 8 naming [:status] [200], `\n[0 000 1 0 0 0]\n`. status101: 16 naming [:status] [101], `\n[0 000 1 0 0 0]\n`.
  - trpseudo, uppertrailer: 16 naming [:status] [200] / [X-T] [1], `HTTP/2 200 \r\n\r\nhello h2\n` + `\n[2 200 1 9 15 0]\n` (REF now 9 too: BL-3664's 10 was the old recorder escape).
  - twoheaders: 16, `HTTP/2 200 \r\n\r\n` + `\n[2 200 1 0 15 0]\n`. trnoes: 16 `nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `HTTP/2 200 \r\n\r\nhello h2` + `\n[2 200 1 8 15 0]\n`.
  - Controls: te: trailers: 0, `[2 200 1 0 29 1]`, GOAWAY error 0, match. Head `x-a: 1` with trailer `x-t: 1`: 0, same bytes, but num_headers REF 1 / PORT 2: with trailers, curl's num_headers is the trailer count alone (also measured: head x-a, x-b + trailer x-t gives REF 1, PORT 3). Not a CGR-135 repro (CGR-273/BL-3802 territory): filed BL-4021.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. CI on Linux and macOS: the new test drives a scripted in-memory connection with no path, OS error or certificate, so it is platform-neutral; fast tests green here. `dotnet format --verify-no-changes` flags only `Cgr218Tests.cs`, which this task did not change.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 7cb8fe79 b61262fc e58d9009 779790c0.
- 2026-10-28: Doing -> Done. Every CGR-135 repro matches curl 8.18.0, cltwo num_headers included
