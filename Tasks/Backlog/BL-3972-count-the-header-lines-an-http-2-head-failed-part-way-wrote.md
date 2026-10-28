---
id: BL-3972
title: Count the header lines an HTTP/2 head failed part way wrote in num_headers (CGR-135 cltwo)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Cgr135NumHeadersTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3972 — Count the header lines an HTTP/2 head failed part way wrote in num_headers (CGR-135 cltwo)

## Goal

When an HTTP/2 head fails part way (nghttp2 rejects a field after valid ones), `%{num_headers}` counts the header lines curl wrote before the failure, as curl 8.18.0 with nghttp2 1.68 does.

## Context

Found by BL-3664 (CGR-135). Server (Record-CurlExchange.ps1 `-Http2Origin prior-knowledge -Http2Frames 'H! :status: 200|content-length: 9|content-length: 9'`), command `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_header} %{num_headers}]\n'`:
- REF-LINUX: exit 16, stdout `HTTP/2 200 \r\ncontent-length: 9\r\n` + `\n[2 200 1 0 32 1]\n`.
- Curl (after BL-3664's stream fix): the same bytes but `[2 200 1 0 32 0]`: `num_headers` is 0.
`Http2StreamConnection` enqueues the status line and the valid lines without the empty line, then the next read throws; `HttpResponseHeadReader.HeadersStoredWith` is only taken for a head read whole (`HttpProtocolHandler.cs` around line 1355), so the lines already parsed are not counted. Check how HTTP/1.1's partial-head path counts too, and keep it as measured.

## Acceptance criteria

- [ ] The command above through Curl gives `[2 200 1 0 32 1]`, as REF-LINUX does; the measurement is recorded in Notes.
- [ ] A focused unit test in `Curl.Protocol.Http.UnitTests/Cgr135NumHeadersTests.cs` fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
