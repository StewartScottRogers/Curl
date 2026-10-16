---
id: BL-2366
title: Fill CURLINFO_SSL_VERIFYRESULT and PROXY_SSL_VERIFYRESULT from the transfer report
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2361]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2366 — Fill CURLINFO_SSL_VERIFYRESULT and PROXY_SSL_VERIFYRESULT from the transfer report

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info.SslVerifyResult` and `Info.ProxySslVerifyResult` hold the certificate verify codes the transfer reported instead of their fresh-handle defaults. (`Info.OsErrno` and `Info.ProxyError`, first planned here, moved to BL-2374 and BL-2375.)

## Context

Left by BL-2361 (ADR-0506 plan phase 2): `TransferReport` carries none of them. The verify results are recorded tool-side today by `Curl.Console`'s `TlsResultRecordingTransferEvents` (what `%{ssl_verify_result}` and `%{proxy_ssl_verify_result}` print). Put each on `TransferReport` and read it in `Curl.Engine`'s `TransferInfoFromReport`, as the `-w` variable of the same name is worked out in `Curl.Output`'s `TransferWriteOutVariables`.

## Acceptance criteria

- [x] `TransferInfoFromReportTests` has one assertion for each of the two verify values filled from a report that carries it (`Of_Report_TakesTheSslVerifyResult`, `Of_Report_TakesTheProxySslVerifyResult`).
- [x] A report that carries none leaves each at its fresh-handle default (0) (`Of_ReportWithoutVerifyResults_AnswersZeroForEach`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary` reports 0 failing members.

## Notes

- Split at the start of the run (before any code): `CURLINFO_OS_ERRNO` and `CURLINFO_PROXY_ERROR` are measured nowhere yet. `ConnectResult` drops the `SocketError` `TcpConnector.DialFailure` has, and errno differs per platform (10061 / 111 / 61 for a refused connect), so it needs its own Networking work: BL-2374. No `CURLPX_*` code is classified anywhere and `%{proxy_error}` is missing from `TransferWriteOutVariables` too (the Context's claim that it is worked out there was wrong): BL-2375. Neither blocks this task; both depend on it, since they extend the same `TransferReport` and `TransferInfoFromReport`.
- Design: the verify codes arrive as `ITransferEvents.ReportCertificateVerifyResult` from `Curl.Networking`, not from the handler, so the engine records them with a new decorator, `TlsVerifyResultRecordingTransferEvents` (the engine's copy of `Curl.Console`'s `TlsResultRecordingTransferEvents`), outermost around the transfer's events, and its `ReportOn` puts them on the finished `TransferResult`'s report (a new report when the handler returned none, e.g. a failed handshake; the result unchanged when nothing was reported or the progress callback aborted). They are not kept on `EngineTransferState`, because `Curl.Console`'s `RunningTransferState` derives from it and already has members of those names.
- `touches` narrowed to what changed: Curl.Networking was not needed once OS errno moved out.
- Quality: Engine 100/100, 0 failing (first run caught `PerformAsync` at complexity 12 from a null check, moved into `ReportOn`); Abstractions 100/100, 0 failing.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Info.SslVerifyResult and ProxySslVerifyResult hold the verify codes the transfer reported; OS_ERRNO and PROXY_ERROR split to BL-2374 and BL-2375
