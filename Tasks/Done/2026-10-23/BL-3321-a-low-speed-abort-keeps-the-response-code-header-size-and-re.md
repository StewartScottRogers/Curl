---
id: BL-3321
title: A low-speed abort keeps the response code, header size and request size libcurl reports
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs, Curl.Engine.UnitTests/AbortedHeadRecordingTransferEventsTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3321 — A low-speed abort keeps the response code, header size and request size libcurl reports

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

lowspeed (/slowbody, CURLOPT_LOW_SPEED_LIMIT 1000000, CURLOPT_LOW_SPEED_TIME 1): libcurl reports RESPONSE_CODE 200, HEADER_SIZE 76, REQUEST_SIZE 62 and NUM_CONNECTS 1; Curl reports 0 for each.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Fix lives in Curl.Engine (touches widened: the cancelled handler returns no report, so the engine records the heads from the transfer events, AbortedHeadRecordingTransferEvents); no other Doing task names these files.
- Differential run (Git ucrt64 libcurl 8.22.0): lowspeed no longer differs in RESPONSE_CODE, HEADER_SIZE, REQUEST_SIZE, NUM_CONNECTS. Left over, filed as a follow-up: primary IP, local port, CURLINFO 0x600008 and 0x20002E.
- Measure-CodeQuality.ps1 Curl.Engine.UnitLibrary: 0 failing members. Linux/macOS: change is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. A low-speed abort keeps the response code, header size, request size and connection count libcurl reports
