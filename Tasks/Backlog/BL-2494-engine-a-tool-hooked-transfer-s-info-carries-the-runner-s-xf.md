---
id: BL-2494
title: Engine: a tool-hooked transfer's Info carries the runner's xfer_id, conn_id, scheme, Referer, verify results and early data
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2452]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2494 — Engine: a tool-hooked transfer's Info carries the runner's xfer_id, conn_id, scheme, Referer, verify results and early data

## Goal

A serial transfer's `-w` output reads every libcurl variable from its easy handle's `curl_easy_getinfo` answers, with no runner overlay in `CurlCommandRunner.WriteOutVariables`.

## Context

ADR-0625 (BL-2452): `-w` now takes the handle's `GetInfo` answers (`EasyHandleWriteOutInfo`), but the runner still lays its own `Scheme` (`WriteOutScheme`), `Referer`, `TransferId`, `ConnectionId`, `SslVerifyResult`, `ProxySslVerifyResult` and `EarlyDataSent` over them, and uses its normalized request URL as the effective URL when the report names none. `ManagedEasyTransfer` numbers `xfer_id` from its own shared `TransferPerformer` (not the runner's) and falls back to `CURLOPT_URL`. Give the tool-hooked engine transfer the runner's numbering and answers (for instance through `EngineToolTransferEvents.State`, which already carries the `conn_id`), then drop the overlay. `-Z` stays on `WriteOutTransferInfo.FromReport` until BL-2453.

## Acceptance criteria

- [ ] `WriteOutVariables` lays no libcurl field over the handle's answers for a serial transfer that reached its handle.
- [ ] Every `-w` variable's output is unchanged, pinned by the existing write-out tests; the fast tests pass and `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
