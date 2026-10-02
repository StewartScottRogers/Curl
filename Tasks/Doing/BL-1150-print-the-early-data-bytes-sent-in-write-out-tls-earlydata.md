---
id: BL-1150
title: Print the early data bytes sent in --write-out tls_earlydata
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1105, BL-906]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1150 — Print the early data bytes sent in --write-out tls_earlydata

## Goal

`-w '%{tls_earlydata}'` prints the number of bytes the transfer sent as TLS 1.3 0-RTT early data (BL-1105), as curl 8.21.0's OpenSSL build does, instead of the fixed 0 it prints today, by carrying the count `ITransferEvents.ReportTlsEarlyData` reports (BL-906) to `TransferWriteOutVariables.TlsEarlyDataSent` in `Curl.Console`.

## Context

- BL-906 (ADR-0043's 2026-10-01 amendment) did everything outside `Curl.Console`: `ITransferEvents.ReportTlsEarlyData(long bytes)` in Abstractions (default no-op), reported by `HandBuiltTlsProvider` after a deferred early-data handshake (bytes sent, negative when the server rejected them, never for a proxy), passed on by `ConnectionOpenedCapturingTransferEvents` and `HandshakeCapturingTransferEvents`, and printed from `TransferWriteOutVariables.TlsEarlyDataSent`. `Curl.Console` was held by BL-923 then, so the last hop was split here.
- Follow BL-661's verify-result path: `VerifyResultRecordingTransferEvents` records `ReportCertificateVerifyResult` on `RunningTransferState`, and `CurlCommandRunner` (around `SslVerifyResult = Running.SslVerifyResult`) copies it into `TransferWriteOutVariables`.
- Every `Curl.Console` decorator between the provider and the recorder must forward `ReportTlsEarlyData`, or the default no-op swallows it: `ConnectionIdRecordingTransferEvents`, `ConnectReplyHeadWritingEvents`, `VerifyResultRecordingTransferEvents` (check `TransferContextFactory` and `CurlCommandRunner` for others).
- curl 8.21.0: `lib/vtls/openssl.c` calls `Curl_pgrsEarlyData` with `earlydata_skip` when accepted and its negation when rejected; `CURLINFO_EARLYDATA_SENT_T` documents the negative value.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test pins `%{tls_earlydata}` as the reported byte count (positive and negative) after a transfer whose events report it, and 0 without.
- [ ] Every `Curl.Console` `ITransferEvents` decorator forwards `ReportTlsEarlyData`, each with a test.
- [ ] The value reaches the write-out through the running transfer's state, not a static.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for Curl.Console.

## Notes

- 2026-10-01 (lane 8, BL-906): narrowed to the `Curl.Console` wiring; the rest landed in BL-906.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
