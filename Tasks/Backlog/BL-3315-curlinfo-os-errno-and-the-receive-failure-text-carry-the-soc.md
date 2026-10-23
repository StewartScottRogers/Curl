---
id: BL-3315
title: CURLINFO_OS_ERRNO and the receive failure text carry the socket error as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3328, BL-3329, BL-3334, BL-3335]
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
- 2026-10-23 (lane 7): Reran LibcurlEasyDifferentialTests after BL-3328/BL-3329/BL-3304 landed (10.5 min, reference Git ucrt64 libcurl-4.dll 8.22.0). proxy-tunnel-http and proxy-connect are now identical (errno and "Recv failure" text). Still differing: get-follow/rother, userpwd-unrestricted, userpwd-redirect-other (`200019=0:10061` vs `0`) and reuse-idle-close (`10054` vs `0`). Causes: (1) `PoolingConnector` rebuilds a new connection's `ConnectResult` without BL-3328's `LastSocketError` (the Curl CLI's trace shows the `::1` refusal and `127.0.0.1` connect, so the race itself matches; libcurl's `::1` refusal takes 7 ms, as does ours with SIO_TCP_INITIAL_RTO). Filed as BL-3334. (2) BL-3329 only puts a failed receive's errno on a failed transfer; a dead reused connection retried on a fresh one drops it. Filed as BL-3335, and HttpProtocolHandler.cs is held by BL-3320 right now. Once both are Done, rerun the differential and tick the criteria (the focused unit tests live in those two tasks).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Backlog. Split: OS_ERRNO needs Abstractions/Networking/Engine/Http work, filed as BL-3328 and BL-3329 (after BL-3304); this task re-verifies the cases afterwards
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Backlog. Waits on BL-3334 (PoolingConnector drops LastSocketError) and BL-3335 (errno of a dead reused connection retried fresh); rerun the differential after
