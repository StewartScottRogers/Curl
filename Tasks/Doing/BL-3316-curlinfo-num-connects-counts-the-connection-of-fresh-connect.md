---
id: BL-3316
title: CURLINFO_NUM_CONNECTS counts the connection of fresh-connect, forbid-reuse, low-speed-aborted and TFTP transfers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs]
requirement: none
created: 2026-10-23
completed:
---# BL-3316 — CURLINFO_NUM_CONNECTS counts the connection of fresh-connect, forbid-reuse, low-speed-aborted and TFTP transfers

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

0x20001A is 1 in libcurl and 0 in Curl for fresh-connect and forbid-reuse (two rounds, each with a new connection), lowspeed and tftp-bad. BL-3302 covers the TLS connect timeout case (tls-notls-server).

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
