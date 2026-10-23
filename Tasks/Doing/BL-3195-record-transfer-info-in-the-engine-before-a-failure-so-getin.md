---
id: BL-3195
title: Record transfer info in the engine before a failure so getinfo and -w keep it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitTests/TransferInfoAfterFailureTests.cs, Curl.Console.UnitTests/CurlCommandRunnerWriteOutAfterProtocolFailureTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3195 — Record transfer info in the engine before a failure so getinfo and -w keep it

## Goal

After a failed transfer, getinfo (and the CLI's `-w`) still report the response code, header size, HTTP version, primary IP and port the transfer reached, and PRIMARY_PORT is -1 after a connection failure, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 24). Repro: a response with a bad header line, a 400 KB header, or two Location headers. Upstream still reports RESPONSE_CODE 200 or 302, HEADER_SIZE, HTTP_VERSION, PRIMARY_IP and PRIMARY_PORT; we report 0 or empty. PRIMARY_PORT after a connection failure is -1 upstream and 0 for us. Same root as report 10 (BL-3170, Done, which fixed only the CLI write-failure path) one level down: the fix belongs in the engine, recording info before it reports the failure.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/TransferInfoAfterFailureTests.cs` runs each of the three repros and expects the upstream RESPONSE_CODE, HEADER_SIZE, HTTP_VERSION, PRIMARY_IP and PRIMARY_PORT together with the upstream exit code.
- [ ] A test shows PRIMARY_PORT is -1 after a refused connect.
- [ ] A CLI test runs the same three repros with `-w '%{http_code} %{size_header} %{http_version} %{remote_ip} %{remote_port}'` and expects curl 8.22.0's measured output.
- [ ] BL-3170's tests still pass. Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: f3678bb8 fc40829c a3fcf835 9d16301f.
