---
id: BL-3192
title: Honour CURLOPT_HTTP_CONTENT_DECODING=0 and HTTP_TRANSFER_DECODING=0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/HttpDecodingOffTests.cs, Curl.Console.UnitTests/CurlCommandRunnerRawTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3192 — Honour CURLOPT_HTTP_CONTENT_DECODING=0 and HTTP_TRANSFER_DECODING=0

## Goal

With `HTTP_CONTENT_DECODING`=0 the body is delivered still encoded, and with `HTTP_TRANSFER_DECODING`=0 a chunked body is delivered with its chunk framing, as upstream does; `--raw` keeps working.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 22). Repro: HttpContentDecoding=0 against a gzip response with a broken body: upstream returns the raw bytes, exit 0; we still decode and fail with exit 61. HttpTransferDecoding=0 against a chunked response: upstream returns the raw chunked stream (46 bytes in Stewart's test); we return the decoded 11 bytes. This is the option the CLI exposes as `--raw`, whose hang (report 2) was fixed by BL-3162 (Done); keep BL-3162's tests passing and add its repro here through the API path too.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/HttpDecodingOffTests.cs` covers CONTENT_DECODING=0 over gzip, bad gzip (exit 0, raw bytes) and an unknown encoding, and TRANSFER_DECODING=0 over a chunked response (raw chunk framing, byte count as measured).
- [ ] The same tests with both options at 1 still decode (and bad gzip still fails with 61).
- [ ] A CLI test runs report 2's `--raw` chunked repro and expects the raw stream and no hang; BL-3162's tests still pass.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
