---
id: BL-2361
title: Measure the CURLINFO_* values TransferInfoFromReport leaves at their defaults
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2317]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2361 — Measure the CURLINFO_* values TransferInfoFromReport leaves at their defaults

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info` also holds the `CURLINFO_*` values BL-2317's `TransferInfoFromReport` could not fill because the engine does not measure them yet: `CURLINFO_XFER_ID`, `CURLINFO_CONN_ID`, `CURLINFO_SSL_VERIFYRESULT`, `CURLINFO_PROXY_SSL_VERIFYRESULT`, `CURLINFO_OS_ERRNO`, `CURLINFO_PROXY_ERROR`, `CURLINFO_PROXYAUTH_USED`, `CURLINFO_RETRY_AFTER`, `CURLINFO_CONTENT_LENGTH_UPLOAD`, `CURLINFO_EARLYDATA_SENT_T`, `CURLINFO_LASTSOCKET`, `CURLINFO_ACTIVESOCKET`, `CURLINFO_COOKIELIST`, `CURLINFO_CERTINFO` and the RTSP four (`CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CLIENT_CSEQ`, `CURLINFO_RTSP_SERVER_CSEQ`, `CURLINFO_RTSP_CSEQ_RECV`).

## Context

ADR-0506, plan phase 2; follows BL-2317 (see its Notes for why each is still at its default). Where each comes from today: the transfer and connection IDs are numbered by `TransferPerformer` and recorded by `Curl.Console`'s tool-side `ConnectionIdRecordingTransferEvents`; the verify results by `TlsResultRecordingTransferEvents` (also tool-side); `Retry-After` is parsed by `Curl.Core`'s retry code for `--retry`; the cookie list is the transfer's `CookieEngine`; `CURLINFO_CERTINFO` needs `CURLOPT_CERTINFO` on `EasyTlsOptions` first and then `Curl.Output`'s `PeerCertificateText` lines split into `name:value` strings. `CURLINFO_CAINFO`, `CURLINFO_CAPATH`, `CURLINFO_SSL_ENGINES` and `CURLINFO_TLS_SSL_PTR` are build constants whose defaults are the Schannel build's answer; set the OpenSSL build's off Windows. A value that needs a type outside `Curl.Engine` may be split into its own task.

## Acceptance criteria

- [ ] Each `CURLINFO_*` named in the Goal is filled by `TransferInfoFromReport` (or `ManagedEasyTransfer`) from what the engine measured, or the task's Notes say why it stays at its default, with a follow-up task ID.
- [ ] `TransferInfoFromReportTests` (or `ManagedEasyTransferTests`) cover every property newly set, one assertion each.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
