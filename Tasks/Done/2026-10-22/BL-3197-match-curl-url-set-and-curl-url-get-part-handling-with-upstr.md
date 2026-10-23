---
id: BL-3197
title: Match curl_url_set and curl_url_get part handling with upstream
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitLibrary/UrlPartText.cs, Curl.Engine.UnitLibrary/UrlHandleParts.cs, Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/UrlPartSetterUpstreamTests.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs, Libcurl/Url/CurlUrlHandle.cs, Libcurl.UnitTests/Url/CurlUrlHandleUpstreamTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3197 — Match curl_url_set and curl_url_get part handling with upstream

## Goal

The URL API's set and get give upstream's results and codes for each case in report 26.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 26 (URL API)). Each a difference from upstream curl_url_set/curl_url_get: (1) setting user, password, options or zone to "" with UrlEncode leaves the part unset (`http://a/`), not `http://@a/` or `http://:@a/`; (2) setting the fragment to "" keeps the `#` and an empty fragment; (3) setting the query to "" with AppendQuery appends `&`; (4) setting the host with UrlEncode rejects space, brackets, `/`, `?`, `#` and non-ASCII with CURLUE_BAD_HOSTNAME (21) instead of percent-encoding; (5) a host longer than 255 characters fails with BAD_HOSTNAME; (6) Set(Scheme, "HTTPS") keeps the case given; (7) `http:a` and `http:\\a\b` return 28 (we return 4 and 32); (8) a malformed port in a URL gives CURLUE_BAD_PORT_NUMBER with "Port number was not a decimal number between 0 and 65535", not MalformedInput; (9) get with UrlDecode loses bytes that are not UTF-8 (`%FF` becomes U+FFFD) where upstream returns the raw byte: decide whether that call gains a byte[] form and record it in an ADR marked "Decided by Claude under Stewart's delegation".

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/UrlPartSetterUpstreamTests.cs` has one test per case (1)-(8) pinned to upstream's URL text and CURLUcode.
- [x] The ADR for case (9) exists and is in the ADR index; if it adds a byte[] get, a test shows `%FF` decodes to byte 0xFF.
- [x] `Libcurl.UnitTests/Url/CurlUrlHandleUpstreamTests.cs` covers cases (1), (4) and (8) through the public handle.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- No libcurl 8.22.0 C toolchain is on the lane machine, so each pin follows report 26's measured upstream answer, checked against upstream's `lib/urlapi.c`: an empty part encoded under `CURLU_URLENCODE` leaves a never-allocated buffer, so the part is unset; a set empty fragment is kept and shown as `#`; `CURLU_APPENDQUERY` with an unencoded "" appends `&`; a host set under `CURLU_URLENCODE` is checked after encoding, so any `%` it gained is `CURLUE_BAD_HOSTNAME`; a set scheme keeps its case (comparisons with it are now case-insensitive); a non-file scheme with no `/` after its colon is `CURLUE_BAD_SLASHES`, checked after the supported-scheme check as upstream does.
- Case (8) already gave `CURLUE_BAD_PORT_NUMBER` for every malformed port tried (`12x`, `99999`, `-1`, `+1`, `0x10`, `1.5`, `%31`); the tests now pin it, and the Libcurl test pins upstream's error text.
- Case (9): ADR-0776 keeps the decoded get a `string` with no `byte[]` overload; raw bytes come from the undecoded get.
- Added `Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs` to touches (no task in Doing on origin/work/dark-factory named it): two older tests pinned the lower-cased scheme and the encoded host that upstream does not give, and now pin upstream's answer.
- `ManagedCurlUrlHandle.Accept` lost its `CURLUE_BAD_SLASHES` fallback: with the slash check in `ParseAbsolute`, a URL with no scheme reaches it only under `CURLU_GUESS_SCHEME` or `CURLU_DEFAULT_SCHEME`.
- Build clean, fast tests green on Windows (44 test projects); the new tests use no paths or OS text, so they are platform-neutral. Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members (Libcurl production code unchanged).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a8cc69e6 061b6cd4 9391f9d4 3639a5e6.
- 2026-10-22: Doing -> Done. curl_url_set and curl_url_get give upstream's results and codes for report 26's cases
