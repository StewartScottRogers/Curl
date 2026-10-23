# ADR-0785: A library handle starts the low-speed watchdog

- Status: Accepted
- Date: 2026-10-23
- Decided by Claude under Stewart's delegation (BL-3196)

## Context

`CURLOPT_LOW_SPEED_LIMIT` and `CURLOPT_LOW_SPEED_TIME` reached `EasyTransferOptions.LowSpeedLimit` and
`LowSpeedTime`, but `ManagedEasyTransfer.ProgressStartingWatchdogsOf` started the watchdogs only when the curl
tool's runner set `ToolProgress` (ADR-0567), so a `Libcurl` or `Curl.Api` handle, alone or in a multi handle, was
never stopped for being too slow.

## Decision

`ProgressStartingWatchdogsOf` starts the `-Y`/`-y` watchdog (`TransferPerformer.StartLowSpeedWatchdog`) for every
handle; the `-m` watchdog stays the runner's, since a library handle's `CURLOPT_TIMEOUT` already ends its perform
another way. A library handle's values are used as set: the CLI's defaults (30 seconds for `-Y` alone, 1 byte per
second for `-y` alone) are filled in by `Curl.Console`'s `EasyOptionsFromCommandLine`, so a library limit or time of
0 watches nothing, as libcurl 8.22.0's `Curl_speedcheck` never aborts with either 0. The failure is the watchdog's,
exit 28 with `Operation too slow. Less than N bytes/sec transferred the last T seconds`, libcurl's `failf` text.

## Consequences

A stalled library transfer ends with `CURLE_OPERATION_TIMEDOUT`, through `curl_easy_perform` and the multi
interface alike. Tests: `Curl.Engine.UnitTests/LowSpeedLimitTests.cs`, `Libcurl.UnitTests/Options/LowSpeedOptionTests.cs`.
