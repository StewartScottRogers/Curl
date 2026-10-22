---
id: BL-2495
title: Engine: a tool-hooked transfer's Info carries the runner's xfer_id, conn_id, scheme, Referer, verify results and early data
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2452]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2495 — Engine: a tool-hooked transfer's Info carries the runner's xfer_id, conn_id, scheme, Referer, verify results and early data

## Goal

A serial transfer's `-w` output reads every libcurl variable from its easy handle's `curl_easy_getinfo` answers, with no runner overlay in `CurlCommandRunner.WriteOutVariables`.

## Context

ADR-0625 (BL-2452): `-w` now takes the handle's `GetInfo` answers (`EasyHandleWriteOutInfo`), but the runner still lays its own `Scheme` (`WriteOutScheme`), `Referer`, `TransferId`, `ConnectionId`, `SslVerifyResult`, `ProxySslVerifyResult` and `EarlyDataSent` over them, and uses its normalized request URL as the effective URL when the report names none. `ManagedEasyTransfer` numbers `xfer_id` from its own shared `TransferPerformer` (not the runner's) and falls back to `CURLOPT_URL`. Give the tool-hooked engine transfer the runner's numbering and answers (for instance through `EngineToolTransferEvents.State`, which already carries the `conn_id`), then drop the overlay. `-Z` stays on `WriteOutTransferInfo.FromReport` until BL-2453.

## Acceptance criteria

- [x] `WriteOutVariables` lays no libcurl field over the handle's answers for a serial transfer that reached its handle.
- [x] Every `-w` variable's output is unchanged, pinned by the existing write-out tests; the fast tests pass and `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- Design (ADR-0654): `EngineTransferState` now carries `TransferId`, `RequestUrl` and the verify results and early data
  (moved down from `RunningTransferState`); `EngineToolTransferEvents` gains `TakeConnectionId`, the runner's numbering.
  Under `ToolEvents`, `ManagedEasyTransfer` answers `xfer_id` (retry's, else transfer's), `conn_id` (-1 before
  connecting, else the retried one when reused, else the state's, else the runner's next kept on the state), the
  effective URL fallback and scheme from `RequestUrl`, `Referer` (report's else `CURLOPT_REFERER`) and the state's TLS
  values. The runner's `KeepRetriedConnectionIdWhenReused` call moved into the engine, which must apply it before it
  fills `Info`. `EasyHandleWriteOutInfo.Read` reads the seven extra answers; `WriteOutVariables` takes them as they are.
- Choice: the request URL is passed from the runner, because the `-G` / `--url-query` query is `Curl.Cli`'s `QueryUrl`
  and the engine never references `Curl.Cli`.
- `-Z` and handle-less transfers keep `FromReport` plus the runner's values (`ReportWriteOutInfo`) until BL-2453.
  Its `Referer` now reads `FromReport`'s copy of the report's (`info.Referer ?? options.Referer`): same value, one
  branch fewer.
- Measured: fast tests green in all 41 projects (Console 2823, Engine 1707, Conformance 3130 passed);
  `PassingUpstreamCases.txt` unchanged; quality 0 failing members, both libraries 100% line and branch. One
  measurement run hit `Curl.Api.UnitTests`' flaky `SendAsync_CancelledWhilePaused_...`, already fixed on the shared
  branch by BL-2557, and passed on the rerun.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 5fee0aaa bedca47c 830717b9.
- 2026-10-16: Doing -> Done. A serial transfer's -w output reads every libcurl variable from its easy handle's getinfo answers, with no runner overlay
