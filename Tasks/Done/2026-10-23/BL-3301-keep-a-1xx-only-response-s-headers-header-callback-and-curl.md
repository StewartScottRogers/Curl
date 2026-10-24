---
id: BL-3301
title: Keep a 1xx-only response's headers: header callback and curl_easy_header after a failed /status/100 transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Libcurl/Headers, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.WeirdHeaderLine.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3301 — Keep a 1xx-only response's headers: header callback and curl_easy_header after a failed /status/100 transfer

## Goal

A transfer whose server sends only a 1xx response and then closes (/status/100) passes the 1xx header block to the header callback and leaves it for curl_easy_header (CURLHE_MISSING, 2, for an absent name), as libcurl 8.22.0 does.

## Context

BL-3237's replay: /status/100 and '/status/100 follow' both end rc=8 in both libraries, but the reference's header callback got 'HTTP/1.1 100 Status' with its Date, Content-Type and Content-Length lines, and its curl_easy_header answers 2 (CURLHE_MISSING); Curl's Libcurl delivered no header bytes and answers 3 (CURLHE_NOHEADERS). Where it lands in Curl.Engine.UnitLibrary is not yet known.

## Acceptance criteria

- [x] The /status/100 header cases of LibcurlEasyDifferentialTests show no difference.
- [x] A focused unit test in the fixed library's own .UnitTests project pins it (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Filed by BL-3237's differential run on 2026-10-23.
- 2026-10-23 lane-2: BL-3280 had already delivered the 1xx head to the header callback and curl_easy_header. The differential (10 min, Git ucrt64 libcurl 8.22.0) still differed on get/get-follow/get-nobody /status/100 in RESPONSE_CODE (100 vs 0), HEADER_SIZE (106 vs 0) and CONTENT_TYPE: the weird-reply failure after a 1xx head carried no status line. Fix in Curl.Protocol.Http.UnitLibrary (touches widened; no other Doing task touches it): the failure now carries StatusLineBeforeFailure and ContentTypeBeforeFailure. Pinned by ExecuteAsync_BodyAfterAnInformationalHead_ReportsThatHeadsCodeSizeAndContentType. Rerun: no status/100 case differs. Http fast tests green (2263); Measure-CodeQuality.ps1 Curl.Protocol.Http.UnitLibrary: 0 failing members. Engine unchanged. Linux/macOS: platform-neutral test.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 124ad975 ebc469ce 0ac4089b.
- 2026-10-23: Doing -> Done. a 1xx-only failed transfer keeps its code, header size and content type; /status/100 differential cases match
