---
id: BL-513
title: Report the retries --retry made in %{num_retries}
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-513 — Report the retries --retry made in %{num_retries}

## Goal

`%{num_retries}` (and its `%{json}` entry) prints how many times `--retry` retried the transfer, as curl 8.21.0 does, instead of a hard-wired 0.

## Context

- Conformance audit 2026-09-28, row 40 (Major; sequenced into the opening queue at Stewart's request).
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `num_retries` to a constant `0` (the `VariableFormatters` table). ADR-0043 recorded that as correct only because `--retry` was not parsed then; it now is (`Curl.Core.UnitLibrary/TransferRetrier.cs`, `RetryPolicy.cs`; `Curl.Console/RetryPolicyMapping.cs`).
- The count has to travel from the retrier to the write-out variables; pick the smallest route (a property on whatever `TransferWriteOutVariables` is built from in `Curl.Console`).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 3`: `--retry 2 --retry-delay 1 -w '%{num_retries}'` against 503, 503, 200 and against three 503s, plus `-w '%{json}'`; stdout and exit code copied into Notes.
- [x] `Curl.Output.UnitTests` pin `num_retries` from the value it is given; `Curl.Console.UnitTests` pin the measured values end to end on a fake `TimeProvider`.
- [x] ADR-0043's `num_retries` line is not edited here (the stale-ADR task covers it); the XML doc on the variable source says where the count comes from.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-28, curl 8.21.0 (Windows) Schannel, `Record-CurlExchange.ps1 -Connections 3`,
  503 = `HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n`, 200 = `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok`,
  args `-s --retry 2 --retry-delay 1 -w <fmt> http://127.0.0.1:18513/`:
  - 503, 503, 200, `%{num_retries}`: stdout `ok2`, exit 0.
  - 503 x3, `%{num_retries}`: stdout `2`, exit 0.
  - 503, 503, 200, `%{json}`: `..."num_redirects":0,"num_retries":2,...`, `"conn_id":2`, `"xfer_id":2`, exit 0.
  - 503 x3, `%{json}`: `"num_retries":2`, `"http_code":503`, `"conn_id":2`, `"xfer_id":2`, exit 0.
- Route (smallest): `RunningTransferState.RetryCount` is incremented in `FollowRetryingAsync`'s
  `retrying` callback, which `TransferRetrier` calls once per retry, and copied onto the new
  `TransferWriteOutVariables.RetryCount` init property. No change to `Curl.Core` was needed, so
  `Curl.Core.UnitLibrary`/`UnitTests` stayed untouched.
- Follow-up filed: BL-799 - curl counts each retry attempt in `%{xfer_id}` and `%{conn_id}` (both 2
  above); Curl still prints the first attempt's numbers. ADR-0043's stale line is BL-664.
- Quality: `Curl.Output.UnitLibrary` and `Curl.Console` 100% line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. %{num_retries} and its %{json} entry print how many times --retry ran the transfer again, as curl 8.21.0 does
