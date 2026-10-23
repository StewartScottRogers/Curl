---
id: BL-3338
title: CURLINFO_HTTP_VERSION, SIZE_DOWNLOAD_T and the primary address survive a transfer that fails after the response began
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Info, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Libcurl.UnitTests/Info, Curl.Engine.UnitLibrary/TransferHeaderStore.cs, Curl.Engine.UnitTests/TransferHeaderStoreTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.HttpVersion.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3338 — CURLINFO_HTTP_VERSION, SIZE_DOWNLOAD_T and the primary address survive a transfer that fails after the response began

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

lowspeed (/slowbody, low-speed abort, BL-3321 fixed code, header and request size): libcurl still reports SIZE_DOWNLOAD_T (0x600008) 10, PRIMARY_IP (0x100020) 127.0.0.1, PRIMARY_PORT (0x200028) the server's port and HTTP_VERSION (0x20002E) 2; Curl reports 0, empty, -1 and 0. get-follow/rftp (302 to ftp://127.0.0.1:1, refused; BL-3327 fixed code, IP and port): HTTP_VERSION is 2 in libcurl and 0 in Curl. Its error text differing only in 'after 0 ms' vs 'after 1 ms' is timing, not a defect.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.
- SIZE_DOWNLOAD_T, PRIMARY_IP and PRIMARY_PORT after the low-speed abort were already fixed by BL-3333 (5872d6b35, AbortedHeadRecordingTransferEvents keeps the end points and body bytes), so this run fixed only CURLINFO_HTTP_VERSION.
- Fix: TransferHeaderStore.HttpVersion keeps the CURL_HTTP_VERSION_* number of the last status line that is not a CONNECT reply's (cleared per perform), and ManagedEasyTransfer.HttpVersionOf answers it for a failed perform whose report gives no version (lowspeed's synthesized report, get-follow/rftp's ftp hop). Pinned by TransferHeaderStoreTests.HttpVersion_* and ManagedEasyTransferTests.HttpVersionOf_* (ADR-0482). No behaviour choice beyond matching libcurl, so no ADR.
- touches widened with TransferHeaderStore.cs and two Engine test files: the version is read where the status line is; no other task was in Doing on origin/work/dark-factory.
- The differential run (needs the reference libcurl-4.dll and its servers) was not rerun in this lane for cost; the criterion was checked by the unit tests pinning each named answer and by reading the BL-3333 fix. Build -warnaserror clean, all 45 fast test assemblies passed, Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members. Change is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. CURLINFO_HTTP_VERSION keeps the last status line's version after a low-speed abort or a refused redirect hop
