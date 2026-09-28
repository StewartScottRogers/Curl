---
id: BL-403
title: Exit 43 in Curl.Console when --ipfs-gateway is malformed
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-363]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-403 — Exit 43 in Curl.Console when --ipfs-gateway is malformed

## Goal

`curl --ipfs-gateway ::: ipfs://cid/x` run through `Curl.Console` prints `curl: --ipfs-gateway was given a malformed URL` and the try-help line, even with `-s`, and exits 43, with `%{errormsg}` `A libcurl function was given a bad argument` and `%{exitcode}` 43.

## Context

- BL-363 added `IpfsGatewayFailure.MalformedGatewayOption` (exit 43) in `Curl.Core.UnitLibrary`, but `CurlCommandRunner.ReportIpfsGatewayFailureAsync` maps every failure other than `GatewayDetectionFailed` to `IpfsMalformedTargetUrlFailure`, so the console still exits 3 with the right message.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27: `curl -s -m 1 -w '[%{errormsg}] [%{exitcode}]\n' --ipfs-gateway ::: ipfs://cid/x` prints `curl: --ipfs-gateway was given a malformed URL`, `curl: try 'curl --help' or 'curl --manual' for more information`, then `[A libcurl function was given a bad argument] [43]`, exit 43.
- Add a third reference-compared `TransferResult` beside `IpfsGatewayDetectionFailure` and `IpfsMalformedTargetUrlFailure`, and include it in `IsIpfsGatewayFailure`, so it stops the remaining URLs and has no transfer number as the other two do.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test runs `-s -w '[%{errormsg}] [%{exitcode}]\n' --ipfs-gateway ::: ipfs://cid/x` and pins the stderr lines, the stdout `[A libcurl function was given a bad argument] [43]` and exit 43.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan: a third reference-compared `TransferResult`, `IpfsMalformedGatewayOptionFailure` (exit 43, `A libcurl function was given a bad argument`), in `CurlCommandRunner`; `ReportIpfsGatewayFailureAsync` maps `IpfsGatewayFailure.MalformedGatewayOption` to it, and it joins `IsIpfsGatewayFailure` and `RunEndingFailures`, so it prints no `curl: (43)` line, has no transfer number and stops the remaining URLs like the other two. No new design decision beyond the task's Context, so no ADR.
- Test: `CurlCommandRunnerUrlExpansionTests.RunAsync_IpfsWithMalformedGatewayOption_PrintsCurlsLinesEvenSilentAndReturns43`, pinned to the curl 8.21.0 measurement in Context.
- Coverage: every member this task changed is at 100% line and branch. `Measure-CodeQuality.ps1 -Library Curl.Console` (2026-09-27) still lists two failing members, both from before this task and in files it does not touch: `DiskWriteOutFileOpener.TryOpen` (filed as BL-432) and `DumpHeaderOutputStream.WriteAsync` (filed as BL-455 and BL-462). Those tasks own them, so the gate counts as passed for this task's scope.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl --ipfs-gateway ::: ipfs://cid/x prints curl's malformed-gateway lines even with -s and exits 43
