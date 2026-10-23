---
id: BL-3315
title: CURLINFO_OS_ERRNO and the receive failure text carry the socket error as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3328, BL-3329]
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.OsErrno.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs]
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

- 2026-10-23 (lane 5): Measured with the differential run (10 min): 0x200019 differs in six cases by two causes, both outside this task's touches. (1) get-follow/rother, userpwd-unrestricted, userpwd-redirect-other: localhost tries ::1 (refused, 10061) then 127.0.0.1 connects; libcurl keeps 10061, Curl has no errno on a successful ConnectResult. Filed as BL-3328. (2) reuse-idle-close (10054) and proxy-tunnel-http (10053): a failed receive sets the errno; filed as BL-3329, which waits on BL-3304 (the "Connection was aborted" text). What is left here: rerun LibcurlEasyDifferentialTests once both land, confirm the named cases are identical, tick the criteria.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Backlog. Split: OS_ERRNO needs Abstractions/Networking/Engine/Http work, filed as BL-3328 and BL-3329 (after BL-3304); this task re-verifies the cases afterwards
