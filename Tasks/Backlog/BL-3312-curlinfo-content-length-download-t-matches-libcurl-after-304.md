---
id: BL-3312
title: CURLINFO_CONTENT_LENGTH_DOWNLOAD_T matches libcurl after 304, conflicting lengths, resume, unmet time condition and aborts
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs]
requirement: none
created: 2026-10-23
completed:
---# BL-3312 — CURLINFO_CONTENT_LENGTH_DOWNLOAD_T matches libcurl after 304, conflicting lengths, resume, unmet time condition and aborts

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

0x60000F differs: get*/notmod 0 vs -1; get*/cl-dup-diff -1 vs 1; get-nobody/cl-and-te 3 vs -1; resume-norange -1 vs 25; timecond-ifunmod and timecond-lastmod 8 vs 0; maxfilesize-hdr and maxfilesize-large -1 vs 3000000; proto-redir-restrict -1 vs 0; ignore-cl -1 vs 100 and ignore-cl-head -1 vs 3; file-dir -1 vs 1199.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
