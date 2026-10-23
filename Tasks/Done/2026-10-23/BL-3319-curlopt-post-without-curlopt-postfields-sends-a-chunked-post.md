---
id: BL-3319
title: CURLOPT_POST without CURLOPT_POSTFIELDS sends a chunked POST from the read callback
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.PostBody.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.ReadCallbackPost.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3319 — CURLOPT_POST without CURLOPT_POSTFIELDS sends a chunked POST from the read callback

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

post-flag-only (CURLOPT_POST 1 only): libcurl sends POST /echo with Transfer-Encoding: chunked, Content-Type: application/x-www-form-urlencoded and Expect: 100-continue, then 0\r\n\r\n (the default read callback reads an empty stdin); Curl sends GET /echo.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- touches narrowed from ManagedEasyTransfer.cs and ManagedEasyTransferTests.HttpPost.cs to the files the logic lives in (ManagedEasyTransfer.PostBody.cs, ManagedEasyTransferTests.ReadCallbackPost.cs).
- Fix: ReadCallbackPostBodyOf posts an empty chunked body (default read callback on empty stdin) when no read callback and PostFieldSize is not 0. Pinned by PerformAsync_PostWithNoReadCallbackAndNoPostFields_PostsAnEmptyChunkedBody (replaced the old SendsNoBody test). The differential run needs Git ucrt64 libcurl (Integration), not run in the lane; the unit test pins the answer. Quality: 0 failing members; Engine and Libcurl fast tests green.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. CURLOPT_POST without POSTFIELDS now sends an empty chunked POST like libcurl
