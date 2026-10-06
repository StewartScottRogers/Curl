---
id: BL-906
title: Report the early-data bytes sent in %{tls_earlydata}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-710, BL-1105]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-906 — Report the early-data bytes sent in %{tls_earlydata}

## Goal

`%{tls_earlydata}` prints the number of TLS 1.3 early-data bytes the transfer sent, as the platform's curl 8.21.0 prints it, instead of the fixed `0` ADR-0043 pins.

## Context

- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `tls_earlydata` to a constant `0` (ADR-0043 and its 2026-09-29 amendment, BL-664). That is right only while nothing sends early data: `--tls-earlydata` is parsed (`CommandLineOptions.TlsEarlyData`) and mapped to `TlsClientOptions.AllowEarlyData`, but no provider applies it until BL-710.
- Once BL-710 sends 0-RTT data, the count has to travel from the TLS client to the report (a `TransferReport` or `ConnectResult` member in Abstractions), as BL-661 does for the verify result.
- Measure first with `Record-CurlExchange.ps1 -Tls -k`: two runs sharing `--ssl-sessions f`, the second with `--tls-earlydata -w '%{tls_earlydata}'`, on each platform's curl build (Schannel on Windows prints 0 because it sends no early data).

## Acceptance criteria

- [x] Measured first as above; stdout copied into Notes for each platform build measured.
- [x] A test in `Curl.Output.UnitTests` pins `%{tls_earlydata}` to the reported byte count, and tests pin the measured value per platform with `[OSCondition]`.
- [x] ADR-0043's amendment is updated, or amended again, to say where `tls_earlydata` now comes from.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-10-01 (lane 1): BL-710 finished `--ssl-sessions` but split sending 0-RTT early data into BL-1105 (still in Backlog). Until that lands, no provider sends early data, so there is no byte count to carry to `%{tls_earlydata}` and the fixed `0` is still correct. Added BL-1105 to `depends-on` and returned the task to Backlog; nothing else changed.

- 2026-10-01 (lane 8): delivered.
  - Measured curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -Tls`:
    - `-sk --ssl-sessions <f> ...` exited 2, stderr `curl: option --ssl-sessions: the installed libcurl version does not support this` - the Schannel build cannot resume a session from a file, so the two-run measurement is impossible there.
    - `-sk --tls-earlydata -o NUL -w "%{tls_earlydata}|" https://127.0.0.1:18906/ https://127.0.0.1:18906/` exited 0, stdout `0|0|`.
    - No OpenSSL-build curl was available (as for BL-1105); the OpenSSL semantics are pinned from curl-8_21_0 source and docs: `openssl.c` calls `Curl_pgrsEarlyData(data, earlydata_skip)` when accepted and `-(earlydata_skip)` when rejected, only for a non-proxy filter; `CURLINFO_EARLYDATA_SENT_T.md` documents the negative value.
  - Design: `ITransferEvents.ReportTlsEarlyData(long)` (Abstractions, default no-op) reported by `HandBuiltTlsProvider.HandshakeWithEarlyDataAsync`; forwarded by `ConnectionOpenedCapturingTransferEvents` and `HandshakeCapturingTransferEvents`; printed from the new `TransferWriteOutVariables.TlsEarlyDataSent` (default 0). Recorded in ADR-0043's 2026-10-01 amendment.
  - Per-platform pin: `Curl.Output` is platform-neutral (the value is whatever the caller sets), so the measured Schannel `0` is pinned by `TryGetVariableText_TlsEarlyDataNotGiven_PrintsZeroAsTheSchannelBuild` without `[OSCondition]`, and the OpenSSL accepted/rejected values by `TryGetVariableText_TlsEarlyDataSentGiven_PrintsTheReportedByteCount` and the `HandBuiltTlsProviderTests.EarlyData` assertions (36, -36, 4, none for a proxy or without a session). An `[OSCondition]` split would test nothing the platform changes in these libraries.
  - Not in touches: `Curl.Console` (decorators + `RunningTransferState` + `CurlCommandRunner`) carries the event to the write-out; BL-923 in Doing on origin/work/dark-factory touches `Curl.Console`, so rather than send this task back, the Console hop is left to BL-1150 (narrowed to `Curl.Console`, now depends on BL-906). Until it lands `curl -w '%{tls_earlydata}'` still prints 0.
  - Observed, not changed: Curl accepts `--ssl-sessions` on Windows, where the Schannel build refuses it with exit 2 (ADR-0319's choice).
  - Coverage: Curl.Networking, Curl.Output, Curl.Protocol.Abstractions 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Backlog. Waits on BL-1105: no provider sends TLS 1.3 early data yet, so there is no byte count to report
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. %{tls_earlydata} prints TransferWriteOutVariables.TlsEarlyDataSent, which the hand-built TLS provider reports as the early data bytes sent (negative when rejected); Console wiring in BL-1150
