---
id: BL-3339
title: CURLINFO_SIZE_UPLOAD_T counts the terminating chunk of an empty chunked POST
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Info/Times/EasyInfoReaders.Times.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.UploadSize.cs, Libcurl.UnitTests/Info]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3339 — CURLINFO_SIZE_UPLOAD_T counts the terminating chunk of an empty chunked POST

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

post-flag-only (CURLOPT_POST 1 alone; BL-3319 made Curl send the empty chunked POST): SIZE_UPLOAD_T (0x600007) is 5 in libcurl (the terminating chunk 0\r\n\r\n) and 0 in Curl. BL-3320 covers chunked upload sizes for post-chunked-header; check whether its fix covers this case first.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.

- 2026-10-23: BL-3320's fix (HttpProtocolHandler UploadSize = upload.BytesSent, chunk framing included) already covers this: the empty chunked POST's closing chunk goes through HttpRequestBodyWriter.WriteLastChunkAsync, which counts it. Measured: LibcurlEasyDifferentialTests against Git's ucrt64 libcurl-4.dll 8.22.0 no longer lists post-flag-only (24-line report). No production code changed, so no library to measure with Measure-CodeQuality.ps1 (0 failing members, vacuously). Added Libcurl.UnitTests/Info/EasyInfoSizeUploadOfEmptyChunkedPostTests.cs pinning SIZE_UPLOAD_T = 5 for CURLOPT_POST 1 alone (ADR-0482); its script answers 100 Continue because the request carries Expect: 100-continue. Build clean with -warnaserror, fast tests green. Platform-neutral (scripted in-memory connection).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. CURLINFO_SIZE_UPLOAD_T of an empty chunked POST is 5, pinned by a Libcurl unit test
