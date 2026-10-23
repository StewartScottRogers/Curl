---
id: BL-3195
title: Record transfer info in the engine before a failure so getinfo and -w keep it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitTests/TransferInfoAfterFailureTests.cs, Curl.Console.UnitTests/CurlCommandRunnerWriteOutAfterProtocolFailureTests.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3195 — Record transfer info in the engine before a failure so getinfo and -w keep it

## Goal

After a failed transfer, getinfo (and the CLI's `-w`) still report the response code, header size, HTTP version, primary IP and port the transfer reached, and PRIMARY_PORT is -1 after a connection failure, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 24). Repro: a response with a bad header line, a 400 KB header, or two Location headers. Upstream still reports RESPONSE_CODE 200 or 302, HEADER_SIZE, HTTP_VERSION, PRIMARY_IP and PRIMARY_PORT; we report 0 or empty. PRIMARY_PORT after a connection failure is -1 upstream and 0 for us. Same root as report 10 (BL-3170, Done, which fixed only the CLI write-failure path) one level down: the fix belongs in the engine, recording info before it reports the failure.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/TransferInfoAfterFailureTests.cs` runs each of the three repros and expects the upstream RESPONSE_CODE, HEADER_SIZE, HTTP_VERSION, PRIMARY_IP and PRIMARY_PORT together with the upstream exit code.
- [x] A test shows PRIMARY_PORT is -1 after a refused connect.
- [x] A CLI test runs the same three repros with `-w '%{http_code} %{size_header} %{http_version} %{remote_ip} %{remote_port}'` and expects curl 8.22.0's measured output.
- [x] BL-3170's tests still pass. Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Measured curl 8.22.0 (mingw, Schannel) with Record-CurlExchange.ps1 on 127.0.0.1:47131, `-w "%{http_code} %{size_header} %{http_version} %{remote_ip} %{remote_port}"`: header without colon exit 8 `200 36 1.1 127.0.0.1 47131`; 400 KB header line exit 100 `200 36 1.1 127.0.0.1 47131`; two Location headers exit 8 `302 34 1.1 127.0.0.1 47131`. Refused connect exit 7, DNS failure exit 6 and even a successful file:// transfer print remote_port -1 and local_port -1.
- The response code, version and header size were known only to the HTTP head reader, so the fix records them there (StatusLineBeforeFailure on HttpTransferException, put on the failure report by HttpProtocolHandler.WithHeadBeforeFailure); the engine adds PRIMARY_PORT -1 for a failed transfer with no port (ManagedEasyTransfer.PrimaryPortOf). Added the three HTTP files and Curl.Output.UnitLibrary/TransferWriteOutVariables.cs (whose %{remote_port}/%{local_port} treated only 0 as no connection) to touches: no task in Doing on origin/work/dark-factory named them. ADR-0769.
- -1 for a successful transfer with no connection (file://) is left at 0, recorded in ADR-0769 as out of scope.
- Engine test uses EndPointScriptedConnector so the peer address is a fake loopback end point. Platform-neutral: no paths or OS text, so CI on Linux and macOS is covered by the fast tests here.
- Build -warnaserror clean, fast tests green (incl. BL-3170 CurlCommandRunnerWriteOutAfterWriteFailureTests), Measure-CodeQuality.ps1 for Engine, Http and Output: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: f3678bb8 fc40829c a3fcf835 9d16301f.
- 2026-10-22: Doing -> Done. getinfo and -w keep the response code, header size, HTTP version and peer after a refused head; PRIMARY_PORT -1 after a failure that reached no peer
