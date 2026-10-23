---
id: BL-3191
title: Send CURLOPT_ACCEPT_ENCODING exactly as given
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/AcceptEncodingWireTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3191 — Send CURLOPT_ACCEPT_ENCODING exactly as given

## Goal

`CURLOPT_ACCEPT_ENCODING` sends exactly the string given as `Accept-Encoding`, the full supported list only for the empty string, and no header for NULL, with `CURLINFO_REQUEST_SIZE` following.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 21). Repro: set it to "gzip". Upstream sends `Accept-Encoding: gzip`; we always send `deflate, gzip, br, zstd`. Check that decoding is still enabled for the given encodings, as upstream does (any value but NULL turns content decoding on).

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/AcceptEncodingWireTests.cs` records the request for "gzip", "identity", "" and NULL and expects the header upstream sends (or none for NULL).
- [ ] Each test checks `REQUEST_SIZE` matches the recorded request length.
- [ ] A gzip response is still decoded when the option is "gzip".
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 84f7e62a 3f91e3bd 69d170d5 3639a5e6.
