---
id: BL-3317
title: CURLINFO_SCHEME and CURLINFO_PROTOCOL after a refused file:// redirect and a missing file match libcurl
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.Scheme.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3317 — CURLINFO_SCHEME and CURLINFO_PROTOCOL after a refused file:// redirect and a missing file match libcurl

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

get-follow/loc-file: libcurl keeps scheme http and protocol 1 (0x100031, 0x200030), Curl answers file and 1024. file-missing: libcurl answers file and 1024, Curl null and 0.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- touches widened to add Curl.Engine.UnitLibrary/TransferInfoFromReport.cs: Of must call the new WithSchemeAndProtocol hook; no other Doing task names it.
- Decision: a redirect refused as "(in redirect)" keeps the request URL's scheme and protocol (libcurl keeps its last connection's handler); a file:// transfer failing with exit 37 has no report and answers its URL's. LibcurlEasyDifferentialTests passed against Git ucrt64 libcurl 8.22.0. Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. CURLINFO_SCHEME and PROTOCOL match libcurl after a refused file:// redirect and a missing file
