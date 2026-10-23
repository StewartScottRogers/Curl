---
id: BL-3192
title: Honour CURLOPT_HTTP_CONTENT_DECODING=0 and HTTP_TRANSFER_DECODING=0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/HttpDecodingOffTests.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs, Curl.Console.UnitTests/CurlCommandRunnerRawTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Abstractions.UnitTests/HttpRequestOptionsTests.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ContentEncodingLimit.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RefusedHeader.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.TrEncoding.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.TransferEncoding.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.cs, Curl.Protocol.Http.UnitTests/HttpResponseBodyReaderRawChunkedTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3192 — Honour CURLOPT_HTTP_CONTENT_DECODING=0 and HTTP_TRANSFER_DECODING=0

## Goal

With `HTTP_CONTENT_DECODING`=0 the body is delivered still encoded, and with `HTTP_TRANSFER_DECODING`=0 a chunked body is delivered with its chunk framing, as upstream does; `--raw` keeps working.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 22). Repro: HttpContentDecoding=0 against a gzip response with a broken body: upstream returns the raw bytes, exit 0; we still decode and fail with exit 61. HttpTransferDecoding=0 against a chunked response: upstream returns the raw chunked stream (46 bytes in Stewart's test); we return the decoded 11 bytes. This is the option the CLI exposes as `--raw`, whose hang (report 2) was fixed by BL-3162 (Done); keep BL-3162's tests passing and add its repro here through the API path too.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/HttpDecodingOffTests.cs` covers CONTENT_DECODING=0 over gzip, bad gzip (exit 0, raw bytes) and an unknown encoding, and TRANSFER_DECODING=0 over a chunked response (raw chunk framing, byte count as measured).
- [x] The same tests with both options at 1 still decode (and bad gzip still fails with 61).
- [x] A CLI test runs report 2's `--raw` chunked repro and expects the raw stream and no hang; BL-3162's tests still pass.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Plan and change (ADR-0778): `HttpRequestOptions.Raw` (set only with both decodings 0) split into `PassesContentCoding` and `PassesTransferCoding`; `HttpRequestOptionsMapping` sets each from its own easy option, the CLI's `--raw` still sets both. `HttpProtocolHandler` decodes Content-Encoding unless `PassesContentCoding` and frames, keeps alive and writes the body by `PassesTransferCoding` where it used `Raw`. No new branch.
- Touches widened (no other Doing task on origin/work/dark-factory names any of these files): `HttpRequestOptions.cs` and `HttpProtocolHandler.cs`, where the one flag lived and was read, and the test files that set or asserted `Raw`. `TransferContextFactory.cs` turned out not to need a change.
- Measurement: a single decoding option cannot be set from the curl tool, so each half's behaviour is read from libcurl 8.22.0's source (`http_ce_skip`, `http_te_skip`) and its output is the half of `--raw` BL-3162 measured with `Record-CurlExchange.ps1` (chunk framing through the zero-length chunk and its trailers, exit 0). The tests pin 34 raw bytes for their own chunked body (`6\r\nhello \r\n5\r\nworld\r\n0\r\nX-T: v\r\n\r\n`), the count of the bytes received, as curl writes; Stewart's 46 was his own body's length. An unknown Content-Encoding with decoding on fails with 61, as before.
- The CLI test runs `--raw` over a `StallingConnector` (the server never closes), bounded by a 30 s `WaitAsync`, so a hang fails it; BL-3162's `HttpResponseBodyReaderRawChunkedTests` pass.
- Gates: `dotnet build -warnaserror` clean; fast tests all green on Windows; tests use scripted connections and byte strings only, so they are platform-neutral. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Http.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: f92e5f0f 6fd8289f 6c282f2c 3639a5e6.
- 2026-10-22: Doing -> Done. CURLOPT_HTTP_CONTENT_DECODING=0 and HTTP_TRANSFER_DECODING=0 each pass their coding through undecoded on their own; --raw unchanged
