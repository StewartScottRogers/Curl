---
id: BL-3322
title: CURLAUTH_ANY and CURLAUTH_ANYSAFE on Windows leave the SSPI failure text in the error buffer as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.Auth.cs]
requirement: none
created: 2026-10-23
completed:
---# BL-3322 — CURLAUTH_ANY and CURLAUTH_ANYSAFE on Windows leave the SSPI failure text in the error buffer as libcurl does

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

authall-any and authall-anysafe (/authall, u:p): the Schannel/SSPI libcurl returns 0 and leaves "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package" in CURLOPT_ERRORBUFFER; Curl's answer differs (see the report). Windows only: pin the other platforms' answer separately.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
