---
id: BL-3320
title: CURLINFO_SIZE_UPLOAD_T and CURLINFO_HEADER_SIZE count a chunked upload as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3331]
touches: [Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ChunkedUploadSize.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3320 — CURLINFO_SIZE_UPLOAD_T and CURLINFO_HEADER_SIZE count a chunked upload as libcurl does

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

post-chunked-header (Transfer-Encoding: chunked header with POSTFIELDS "x"): 0x600007 is 11 in libcurl (the chunk framing counted) and 1 in Curl; 0x20000C is 147 vs 137.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- 2026-10-23: Upload size fixed (HttpProtocolHandler Report.UploadSize = upload.BytesSent, chunk framing included; two stdin upload tests repinned to 15 and 200036; new test HttpProtocolHandlerTests.ChunkedUploadSize.cs); differential report no longer lists 0x600007 for post-chunked-header. 0x20000C (147 vs 137) remains with equal wire bytes: filed BL-3331. Http tests green (2259). Code left uncommitted (stashed); Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary still to run on the next claim.

- 2026-10-23: Second claim: BL-3331 (Done) had already fixed 0x20000C (it is CURLINFO_REQUEST_SIZE) by counting upload.BytesSent. Re-applied the stashed UploadSize = upload.BytesSent fix, repinned the two Upload tests (15, 200036), and added ExecuteAsync_ChunkedPostOfOneByte_CountsTheTenFramingBytesInTheUploadSize beside BL-3331's request-size test in HttpProtocolHandlerTests.ChunkedUploadSize.cs (the stashed copy of that file was superseded). WriteOutTransferInfo.cs and TransferInfoFromReportTests.cs needed no change. The first claim measured 0x600007 gone from the differential report with this same change. Build clean, fast tests green, Measure-CodeQuality Curl.Protocol.Http.UnitLibrary: 0 failing members. Platform-neutral (pure in-memory bytes).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Backlog. Waits on BL-3331 (header size 147 vs 137 unexplained); upload-size fix left uncommitted
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. CURLINFO_SIZE_UPLOAD_T counts a chunked upload's framing as libcurl does
