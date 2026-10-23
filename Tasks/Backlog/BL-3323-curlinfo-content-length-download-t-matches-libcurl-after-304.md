---
id: BL-3323
title: CURLINFO_CONTENT_LENGTH_DOWNLOAD_T matches libcurl after 304, conflicting lengths, resume, unmet time condition and aborts
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3316]
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Curl.Engine.UnitTests/TransferInfoLengthAndConditionTests.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.cs]
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

- Run 1 (lane 7): fixed in TransferInfoFromReport.LengthAndCondition.cs and pinned in TransferInfoLengthAndConditionTests (touches changed from TransferInfoFromReportTests.cs, which holds no length tests): any 304 -> 0; HEAD ignores chunked; unmet time condition -> the header length; -1 after exits 1, 22, 33, 36, 63; file:// directory -> -1. Measured with LibcurlEasyDifferentialTests (10 min a run): notmod, cl-and-te, resume-norange, timecond-*, maxfilesize-*, proto-redir-restrict and file-dir now match. Code left uncommitted; the shift stashes it.
- Left: cl-dup-diff (ref -1, ours 1) because ManagedEasyTransfer.Finish.cs line ~273 overwrites the length with the header store's headers.ContentLength (the first of the duplicate lengths); ignore-cl and ignore-cl-head (ref -1, ours 100/3) because CURLOPT_IGNORE_CONTENT_LENGTH is not passed to TransferInfoFromReport.Of (add a parameter from options.Http.IgnoreContentLength, answer -1). Both need Finish.cs, which BL-3316 (Doing) holds, so depends-on BL-3316.
- Do not widen the failure rule: a blanket -1 on any failure broke bad-gzip (15), unknown-encoding (4), maxredirs (0) and gziptrail, where libcurl keeps the header length.
## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Backlog. Finish.cs is held by BL-3316 (Doing); cl-dup-diff and ignore-cl cases need it - depends-on BL-3316
