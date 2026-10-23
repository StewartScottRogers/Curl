---
id: BL-3321
title: A low-speed abort keeps the response code, header size and request size libcurl reports
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs]
requirement: none
created: 2026-10-23
completed:
---# BL-3321 — A low-speed abort keeps the response code, header size and request size libcurl reports

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

lowspeed (/slowbody, CURLOPT_LOW_SPEED_LIMIT 1000000, CURLOPT_LOW_SPEED_TIME 1): libcurl reports RESPONSE_CODE 200, HEADER_SIZE 76, REQUEST_SIZE 62 and NUM_CONNECTS 1; Curl reports 0 for each.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
