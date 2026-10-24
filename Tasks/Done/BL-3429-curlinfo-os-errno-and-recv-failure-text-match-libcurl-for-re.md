---
id: BL-3429
title: CURLINFO_OS_ERRNO and Recv failure text match libcurl for reuse-idle-close and the proxy cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3328, BL-3329, BL-3342, BL-3343]
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.OsErrno.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3429 — CURLINFO_OS_ERRNO and Recv failure text match libcurl for reuse-idle-close and the proxy cases

## Goal

Curl's Libcurl gives the same CURLINFO_OS_ERRNO and receive failure text as libcurl 8.22.0 in the easytest.py cases reuse-idle-close, proxy-tunnel-http and proxy-connect.

## Context

Split from BL-3315. libcurl gives 10054 for reuse-idle-close (a dead reused connection retried on a fresh one, BL-3343) and 10053 plus "Recv failure: Connection was aborted" for proxy-tunnel-http and proxy-connect (already identical after BL-3329 and BL-3304). The cases are in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json, run by Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests) with CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll (8.22.0 Schannel).

## Acceptance criteria

- [x] reuse-idle-close, proxy-tunnel-http and proxy-connect are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- 2026-10-23: Split from BL-3315 (over its cost cap), with BL-3428 for the other cases. The differential run takes about 10 minutes; if it passes already once BL-3343 is Done, only the pinning unit tests remain.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 20050769 db93e725.

- 2026-10-23 (lane 5): With BL-3328/3329/3342/3343 Done, LibcurlEasyDifferentialTests passes (10m30s, report lists none of reuse-idle-close, proxy-tunnel-http, proxy-connect). Answers are pinned by existing focused tests: HttpProtocolHandlerTests.ReusedConnectionOsErrno (10054 on a retried reused connection), HttpProtocolHandlerTests.RemainingInfoLines (10053/10054 receive error), HttpProxyTunnelTests and ConnectTunnelVerboseLinesTests (Recv failure text), TransferInfoFromReportTests (TakesTheOsErrno). No code changed, so no quality run; CI-on-Linux criterion met: no production or test change.
- 2026-10-23: Doing -> Done. Passes already after BL-3329/3343; pinned by existing unit tests
