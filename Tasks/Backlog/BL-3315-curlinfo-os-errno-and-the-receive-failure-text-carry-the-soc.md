---
id: BL-3315
title: CURLINFO_OS_ERRNO and the receive failure text carry the socket error as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs]
requirement: none
created: 2026-10-23
completed:
---# BL-3315 — CURLINFO_OS_ERRNO and the receive failure text carry the socket error as libcurl does

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

0x200019 is 10061 in libcurl and 0 in Curl after a refused redirect target (get-follow/rother, userpwd-unrestricted, userpwd-redirect-other), 10054 vs 0 for reuse-idle-close, 10053 vs 0 for proxy-tunnel-http. proxy-tunnel-http and the header case proxy-connect: libcurl says "Recv failure: Connection was aborted", Curl "Recv failure: Connection was reset". BL-3304 words a kept connection's receive; check it first.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
