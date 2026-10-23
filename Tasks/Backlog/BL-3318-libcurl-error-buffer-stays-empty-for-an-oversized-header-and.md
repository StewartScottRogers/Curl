---
id: BL-3318
title: Libcurl error buffer stays empty for an oversized header and a gzip trailer write error, and names a bad port as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Easy/EasyErrorBuffer.cs, Libcurl/Easy/CurlEasy.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs]
requirement: none
created: 2026-10-23
completed:
---# BL-3318 — Libcurl error buffer stays empty for an oversized header and a gzip trailer write error, and names a bad port as libcurl does

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

get*/hugehdr: both return 100 (CURLE_TOO_LARGE), libcurl leaves CURLOPT_ERRORBUFFER empty, Curl writes "A value or data field grew larger than allowed". enc-empty/gziptrail and enc-gzip-only/gziptrail: both return 23, libcurl leaves it empty, Curl writes "Failed writing received data to disk...". badport (http://127.0.0.1:99999/): libcurl "URL rejected: Port number was not a decimal number between 0 and 65535", Curl "URL rejected: Malformed input to a URL function".

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
