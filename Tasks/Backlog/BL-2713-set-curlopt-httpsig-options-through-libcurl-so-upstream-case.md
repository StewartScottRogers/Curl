---
id: BL-2713
title: Set CURLOPT_HTTPSIG options through Libcurl so upstream cases 5000 and 5004 pass
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2713 — Set CURLOPT_HTTPSIG options through Libcurl so upstream cases 5000 and 5004 pass

## Goal

`curl_easy_setopt` in Libcurl takes `CURLOPT_HTTPSIG_ALGORITHM`, `CURLOPT_HTTPSIG_KEY`, `CURLOPT_HTTPSIG_KEYID` and `CURLOPT_HTTPSIG_HEADERS` (curl 8.22.0), so the ported programs lib5000 and lib5004 sign their requests and upstream cases 5000 and 5004 pass.

## Context

Found by BL-2552. `Libcurl/Options/CurlOption.cs` names the four options, but no setter in `Libcurl/Options/*Setters*.cs` handles them, so `curl_easy_setopt` answers `CURLE_NOT_BUILT_IN` (4). Running case 5000 or 5004 through the runner today gives `<verify><protocol> differs at byte 0 (line 1): expected "GET /5000/resource HTTP/1.1\r\n"` (5004: `"POST /5004/foo HTTP/1.1\r\n"`), got the end, because the program stops at its first `easy_setopt`. The engine already signs requests from `EasyOptions.Authentication.HttpSignatureAlgorithm`, `HttpSignatureKey`, `HttpSignatureKeyId` and `HttpSignatureHeaders` (`Curl.Engine.UnitLibrary/HttpSignatureSetup.cs`, set from the command line in `Curl.Console/EasySetoptsFromCommandLine.TlsAuthenticationProxy.cs`), so the setters only need to map `CURLHTTPSIG_NONE` (0), `CURLHTTPSIG_ED25519` (1) and `CURLHTTPSIG_HMAC_SHA256` (2) and the three strings onto those fields. Case 5004 also sets `CURL_HTTPSIG_CREATED` in `<setenv>`. `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs`'s `Lib5000And5004_StopAtTheSignatureAlgorithmLibcurlDoesNotBuildIn` pins today's `CURLE_NOT_BUILT_IN`; change it to check the signing options reach the transfer.

## Acceptance criteria

- [ ] `curl_easy_setopt` with each of the four `CURLOPT_HTTPSIG_*` options answers `CURLE_OK` for a valid value and sets the matching `EasyOptions.Authentication` field, with unit tests in `Libcurl.UnitTests`; an algorithm number outside 0 to 2 answers `CURLE_BAD_FUNCTION_ARGUMENT`.
- [ ] `Lib5000And5004_StopAtTheSignatureAlgorithmLibcurlDoesNotBuildIn` is replaced by a test that checks the signing options reach the transfer.
- [ ] Upstream cases 5000 and 5004 pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or each one still failing is filed as a task with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass; `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
