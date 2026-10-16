---
id: BL-2366
title: Fill CURLINFO_SSL_VERIFYRESULT, PROXY_SSL_VERIFYRESULT, OS_ERRNO and PROXY_ERROR from the transfer report
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2361]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2366 — Fill CURLINFO_SSL_VERIFYRESULT, PROXY_SSL_VERIFYRESULT, OS_ERRNO and PROXY_ERROR from the transfer report

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info.SslVerifyResult`, `Info.ProxySslVerifyResult`, `Info.OsErrno` and `Info.ProxyError` hold what the transfer measured instead of their fresh-handle defaults.

## Context

Left by BL-2361 (ADR-0506 plan phase 2): `TransferReport` carries none of them. The verify results are recorded tool-side today by `Curl.Console`'s `TlsResultRecordingTransferEvents` (what `%{ssl_verify_result}` and `%{proxy_ssl_verify_result}` print); `CURLINFO_OS_ERRNO` is the socket error of a failed connect (`Curl.Networking`'s connect result) and `CURLINFO_PROXY_ERROR` the `CURLPX_*` code of a failed SOCKS or HTTPS proxy handshake (`%{proxy_error}`). Put each on `TransferReport` (or the result) where it is measured and read it in `Curl.Engine`'s `TransferInfoFromReport`, as the `-w` variable of the same name is worked out in `Curl.Output`'s `TransferWriteOutVariables`.

## Acceptance criteria

- [ ] `TransferInfoFromReportTests` has one assertion for each of the four values filled from a report that carries it.
- [ ] A report that carries none leaves each at its fresh-handle default (0).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
