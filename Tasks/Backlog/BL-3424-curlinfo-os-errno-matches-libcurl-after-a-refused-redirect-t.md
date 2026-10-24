---
id: BL-3424
title: CURLINFO_OS_ERRNO matches libcurl after a refused redirect target (get-follow/rother, userpwd cases)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3328, BL-3329, BL-3342, BL-3343]
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.OsErrno.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3424 — CURLINFO_OS_ERRNO matches libcurl after a refused redirect target (get-follow/rother, userpwd cases)

## Goal

Curl's Libcurl gives the same CURLINFO_OS_ERRNO (0x200019) as libcurl 8.22.0 in the easytest.py cases get-follow/rother, userpwd-unrestricted and userpwd-redirect-other.

## Context

Split from BL-3315. libcurl keeps 10061 (the refused ::1 attempt) while 127.0.0.1 connects; Curl said 0 because PoolingConnector dropped LastSocketError (BL-3342). The cases are in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json, run by Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests) with CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll (8.22.0 Schannel).

## Acceptance criteria

- [ ] get-follow/rother, userpwd-unrestricted and userpwd-redirect-other are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- 2026-10-23: Split from BL-3315 (over its cost cap), with BL-3425 for the other cases. The differential run takes about 10 minutes; if it passes already once BL-3342 is Done, only the pinning unit tests remain.

## Log

- 2026-10-23: Created.
