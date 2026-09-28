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
completed:
---
# BL-403 — Exit 43 in Curl.Console when --ipfs-gateway is malformed

## Goal

`curl --ipfs-gateway ::: ipfs://cid/x` run through `Curl.Console` prints `curl: --ipfs-gateway was given a malformed URL` and the try-help line, even with `-s`, and exits 43, with `%{errormsg}` `A libcurl function was given a bad argument` and `%{exitcode}` 43.

## Context

- BL-363 added `IpfsGatewayFailure.MalformedGatewayOption` (exit 43) in `Curl.Core.UnitLibrary`, but `CurlCommandRunner.ReportIpfsGatewayFailureAsync` maps every failure other than `GatewayDetectionFailed` to `IpfsMalformedTargetUrlFailure`, so the console still exits 3 with the right message.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27: `curl -s -m 1 -w '[%{errormsg}] [%{exitcode}]\n' --ipfs-gateway ::: ipfs://cid/x` prints `curl: --ipfs-gateway was given a malformed URL`, `curl: try 'curl --help' or 'curl --manual' for more information`, then `[A libcurl function was given a bad argument] [43]`, exit 43.
- Add a third reference-compared `TransferResult` beside `IpfsGatewayDetectionFailure` and `IpfsMalformedTargetUrlFailure`, and include it in `IsIpfsGatewayFailure`, so it stops the remaining URLs and has no transfer number as the other two do.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test runs `-s -w '[%{errormsg}] [%{exitcode}]\n' --ipfs-gateway ::: ipfs://cid/x` and pins the stderr lines, the stdout `[A libcurl function was given a bad argument] [43]` and exit 43.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
