---
id: BL-3338
title: CURLINFO_HTTP_VERSION, SIZE_DOWNLOAD_T and the primary address survive a transfer that fails after the response began
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Info, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Libcurl.UnitTests/Info]
requirement: none
created: 2026-10-23
completed:
---
# BL-3338 — CURLINFO_HTTP_VERSION, SIZE_DOWNLOAD_T and the primary address survive a transfer that fails after the response began

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

lowspeed (/slowbody, low-speed abort, BL-3321 fixed code, header and request size): libcurl still reports SIZE_DOWNLOAD_T (0x600008) 10, PRIMARY_IP (0x100020) 127.0.0.1, PRIMARY_PORT (0x200028) the server's port and HTTP_VERSION (0x20002E) 2; Curl reports 0, empty, -1 and 0. get-follow/rftp (302 to ftp://127.0.0.1:1, refused; BL-3327 fixed code, IP and port): HTTP_VERSION is 2 in libcurl and 0 in Curl. Its error text differing only in 'after 0 ms' vs 'after 1 ms' is timing, not a defect.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.

## Log

- 2026-10-23: Created.
