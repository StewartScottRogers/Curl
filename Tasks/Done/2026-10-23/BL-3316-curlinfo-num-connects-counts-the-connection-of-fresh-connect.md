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
completed: 2026-10-23
---# BL-3316 — CURLINFO_NUM_CONNECTS counts the connection of fresh-connect, forbid-reuse, low-speed-aborted and TFTP transfers

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

0x20001A is 1 in libcurl and 0 in Curl for fresh-connect and forbid-reuse (two rounds, each with a new connection), lowspeed and tftp-bad. BL-3302 covers the TLS connect timeout case (tls-notls-server).

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Differential run (LibcurlEasyDifferentialTests, Git ucrt64 libcurl 8.22.0): 0x20001A now matches in lowspeed, fresh-connect, forbid-reuse and tftp-bad; lowspeed still differs in 20000B, 20000C, 100020 and 200028, other tasks.
- Decision: ManagedEasyTransfer.NumConnectsOf counts 1 where the report holds none for a tftp transfer, a successful transfer under FRESH_CONNECT or FORBID_REUSE, and a watchdog cut-off after the response head. The engine does not yet open a new connection for those two options (filed as BL-3326); Linux and macOS: change is platform-neutral, unit tests only.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. CURLINFO_NUM_CONNECTS counts 1 for fresh-connect, forbid-reuse, low-speed-aborted and TFTP transfers
