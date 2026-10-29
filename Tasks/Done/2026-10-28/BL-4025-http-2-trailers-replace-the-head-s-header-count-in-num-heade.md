---
id: BL-4025
title: HTTP/2 trailers replace the head's header count in num_headers, as curl 8.18.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Http2TrailerNumHeadersTests.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/TransferWriteOutVariablesTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4025 — HTTP/2 trailers replace the head's header count in num_headers, as curl 8.18.0 does

## Goal

When an HTTP/2 response ends with trailers, `%{num_headers}` is the number of trailer fields, not the head's header count plus the trailers, as curl 8.18.0 with nghttp2 1.68 gives.

## Context

Found by BL-4005 (CGR-135 controls). Server: `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames <frames> -Response 'HTTP/1.1 200 OK\r\n\r\n'`; command `curl -sS --http2-prior-knowledge -i http://HOST:P/x -w '\n[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_header} %{num_headers}]\n'`. REF-LINUX (curl 8.18.0, nghttp2 1.68, run through `wsl.exe -e curl` with `-ListenAddress 172.26.96.1`) against Curl, 2026-10-28:
- `'H :status: 200|x-a: 1|x-b: 2','D hello h2\n','H! x-t: 1'`: REF `[2 200 1 9 31 1]`, Curl `[2 200 1 9 31 3]`.
- `'H :status: 200|x-a: 1','D hello h2\n','H! x-t: 1'`: REF `[... 23 1]`, Curl `[... 23 2]`.
- `'H :status: 200','D hello h2\n','H! x-t: 1|x-u: 2'`: both `[... 15 2]`; `'H :status: 200','D hello h2\n','H! x-t: 1'`: both `[... 15 1]` (why CGR-273/BL-3802's case, a head with no regular field, matched).
Write-out's `num_headers` is `TransferReport.ResponseHeaders.Count + PseudoHeaders.Count` (`Curl.Output.UnitLibrary/TransferWriteOutVariables.cs`); `HttpProtocolHandler.WithHead` puts the head's headers and the stream trailers in `ResponseHeaders`. Check `%header{}` and `%{header_json}` keep what they show (measure them against REF too) before changing what `ResponseHeaders` holds; if they need the head's fields, count num_headers separately.

## Acceptance criteria

- [x] The four frame sets above give REF's `num_headers` through Curl; the measurement is recorded in Notes.
- [x] A focused unit test in `Curl.Protocol.Http.UnitTests/Http2TrailerNumHeadersTests.cs` fails before the fix and passes after it.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured (REF-LINUX curl 8.18.0, in the Context above): the four frame sets give num_headers 1, 1, 2, 1. Curl now matches, pinned by Http2TrailerNumHeadersTests (four DataRows). Added TransferReport.TrailerCount; num_headers prints it when above zero, ResponseHeaders keeps head and trailers so %header{} and header_json are unchanged. touches widened to TransferReport.cs and the write-out variables (no other Doing task holds them).
- CI criteria met by platform-neutral tests; Measure-CodeQuality Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ccc02e86 d6a2a0b6 3bf8be13 779790c0.
- 2026-10-28: Doing -> Done. HTTP/2 trailers now replace the head count in num_headers, matching curl 8.18.0
