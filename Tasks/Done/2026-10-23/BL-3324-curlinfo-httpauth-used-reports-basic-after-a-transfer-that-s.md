---
id: BL-3324
title: CURLINFO_HTTPAUTH_USED reports Basic after a transfer that sent Basic credentials
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.AuthSchemePicked.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.Auth.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3313 — CURLINFO_HTTPAUTH_USED reports Basic after a transfer that sent Basic credentials

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

0x200045 is 1 in libcurl and 0 in Curl for userpwd-basic, userpwd-basic-explicit, userpwd-wrong, userpwd-nocolon, username-password, userpwd-url, userpwd-auth-redirect, userpwd-unrestricted, userpwd-redirect-other and get-follow/loc-userinfo.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Cause was in the Http handler: AuthSchemePickedBy reported None when Basic was sent before any challenge. Fixed there (added to touches; no Doing task touches Curl.Protocol.Http).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. Http handler now reports Basic picked when Basic is sent before any challenge; unit test added
