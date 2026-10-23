---
id: BL-3190
title: Honour CURLOPT_POSTFIELDSIZE and POSTFIELDSIZE_LARGE for the body and Content-Length
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/PostFieldsTrailerStream.cs, Curl.Engine.UnitTests/PostFieldSizeWireTests.cs, Libcurl.UnitTests/Options/PostFieldSizeOptionTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3190 — Honour CURLOPT_POSTFIELDSIZE and POSTFIELDSIZE_LARGE for the body and Content-Length

## Goal

`CURLOPT_POSTFIELDSIZE(_LARGE)` decides exactly how many bytes of `CURLOPT_POSTFIELDS` are sent and the `Content-Length`, and `CURLINFO_SIZE_UPLOAD(_T)` matches what went on the wire.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 20). Data-integrity bug. Repro: Postfields="abcdef", Postfieldsize=3: upstream sends `Content-Length: 3` and body `abc`; we send all six bytes with `Content-Length: 6`. Size 0 must send an empty body; -1 means "use the string length". Measure upstream for a size larger than the data (10): libcurl reads past the buffer in C, so pin what is safe and closest (record the choice in an ADR marked "Decided by Claude under Stewart's delegation" if it differs from upstream).

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/PostFieldSizeWireTests.cs` records the request bytes for sizes 0, 3, 6, 10 and -1 against `abcdef`, and expects the measured `Content-Length` and body for each, for both POSTFIELDSIZE and POSTFIELDSIZE_LARGE.
- [ ] Each test also checks `SIZE_UPLOAD_T` equals the body bytes actually sent.
- [ ] `Libcurl.UnitTests/Options/PostFieldSizeOptionTests.cs` sets the options through `CurlEasy` and checks the same wire result for size 3.
- [ ] Build clean with -warnaserror, fast tests of the changed projects pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 8449ae23 a94c59ad fd52bbae b44e2f5d.
