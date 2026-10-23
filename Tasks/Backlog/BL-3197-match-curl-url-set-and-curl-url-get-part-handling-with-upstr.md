---
id: BL-3197
title: Match curl_url_set and curl_url_get part handling with upstream
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitLibrary/UrlPartText.cs, Curl.Engine.UnitLibrary/UrlHandleParts.cs, Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/UrlPartSetterUpstreamTests.cs, Libcurl/Url/CurlUrlHandle.cs, Libcurl.UnitTests/Url/CurlUrlHandleUpstreamTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3197 — Match curl_url_set and curl_url_get part handling with upstream

## Goal

The URL API's set and get give upstream's results and codes for each case in report 26.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 26 (URL API)). Each a difference from upstream curl_url_set/curl_url_get: (1) setting user, password, options or zone to "" with UrlEncode leaves the part unset (`http://a/`), not `http://@a/` or `http://:@a/`; (2) setting the fragment to "" keeps the `#` and an empty fragment; (3) setting the query to "" with AppendQuery appends `&`; (4) setting the host with UrlEncode rejects space, brackets, `/`, `?`, `#` and non-ASCII with CURLUE_BAD_HOSTNAME (21) instead of percent-encoding; (5) a host longer than 255 characters fails with BAD_HOSTNAME; (6) Set(Scheme, "HTTPS") keeps the case given; (7) `http:a` and `http:\\a\b` return 28 (we return 4 and 32); (8) a malformed port in a URL gives CURLUE_BAD_PORT_NUMBER with "Port number was not a decimal number between 0 and 65535", not MalformedInput; (9) get with UrlDecode loses bytes that are not UTF-8 (`%FF` becomes U+FFFD) where upstream returns the raw byte: decide whether that call gains a byte[] form and record it in an ADR marked "Decided by Claude under Stewart's delegation".

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/UrlPartSetterUpstreamTests.cs` has one test per case (1)-(8) pinned to upstream's URL text and CURLUcode.
- [ ] The ADR for case (9) exists and is in the ADR index; if it adds a byte[] get, a test shows `%FF` decodes to byte 0xFF.
- [ ] `Libcurl.UnitTests/Url/CurlUrlHandleUpstreamTests.cs` covers cases (1), (4) and (8) through the public handle.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
