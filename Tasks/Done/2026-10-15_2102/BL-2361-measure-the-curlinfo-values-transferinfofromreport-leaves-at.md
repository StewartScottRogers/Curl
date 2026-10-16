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
completed: 2026-10-15
---
# BL-2361 — Measure the CURLINFO_* values TransferInfoFromReport leaves at their defaults

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info` also holds the `CURLINFO_*` values BL-2317's `TransferInfoFromReport` could not fill because the engine does not measure them yet: `CURLINFO_XFER_ID`, `CURLINFO_CONN_ID`, `CURLINFO_SSL_VERIFYRESULT`, `CURLINFO_PROXY_SSL_VERIFYRESULT`, `CURLINFO_OS_ERRNO`, `CURLINFO_PROXY_ERROR`, `CURLINFO_PROXYAUTH_USED`, `CURLINFO_RETRY_AFTER`, `CURLINFO_CONTENT_LENGTH_UPLOAD`, `CURLINFO_EARLYDATA_SENT_T`, `CURLINFO_LASTSOCKET`, `CURLINFO_ACTIVESOCKET`, `CURLINFO_COOKIELIST`, `CURLINFO_CERTINFO` and the RTSP four (`CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CLIENT_CSEQ`, `CURLINFO_RTSP_SERVER_CSEQ`, `CURLINFO_RTSP_CSEQ_RECV`).

## Context

ADR-0506, plan phase 2; follows BL-2317 (see its Notes for why each is still at its default). Where each comes from today: the transfer and connection IDs are numbered by `TransferPerformer` and recorded by `Curl.Console`'s tool-side `ConnectionIdRecordingTransferEvents`; the verify results by `TlsResultRecordingTransferEvents` (also tool-side); `Retry-After` is parsed by `Curl.Core`'s retry code for `--retry`; the cookie list is the transfer's `CookieEngine`; `CURLINFO_CERTINFO` needs `CURLOPT_CERTINFO` on `EasyTlsOptions` first and then `Curl.Output`'s `PeerCertificateText` lines split into `name:value` strings. `CURLINFO_CAINFO`, `CURLINFO_CAPATH`, `CURLINFO_SSL_ENGINES` and `CURLINFO_TLS_SSL_PTR` are build constants whose defaults are the Schannel build's answer; set the OpenSSL build's off Windows. A value that needs a type outside `Curl.Engine` may be split into its own task.

## Acceptance criteria

- [x] Each `CURLINFO_*` named in the Goal is filled by `TransferInfoFromReport` (or `ManagedEasyTransfer`) from what the engine measured, or the task's Notes say why it stays at its default, with a follow-up task ID.
- [x] `TransferInfoFromReportTests` (or `ManagedEasyTransferTests`) cover every property newly set, one assertion each.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- Filled here:
  - `CURLINFO_XFER_ID` was already filled by BL-2359 (`ManagedEasyTransferTests.PerformAsync_TwoHandlesOfOneEngine_ShareTheCookiesAndThePooledConnection` asserts it).
  - `CURLINFO_RETRY_AFTER`: `TransferInfoFromReport` reads the last response's last `Retry-After` with `Curl.Core`'s `RetryAfterHeader.ParseSeconds` (dates against the transfer's clock, capped at six hours), whatever the response code, as libcurl's `http_header_r` stores it. Zero when absent. Choice: only the final response's headers are read; a `Retry-After` on an earlier redirect hop is not carried (libcurl keeps it across hops). Rare; left as is.
  - `CURLINFO_CONTENT_LENGTH_UPLOAD`: `ManagedEasyTransfer.ContentLengthUploadOf` - `CURLOPT_INFILESIZE` under `CURLOPT_UPLOAD`, else a post's `CURLOPT_POSTFIELDSIZE` (or the `CURLOPT_POSTFIELDS` length when the size is -1), else -1. Choice (not measured: the curl tool has no `-w` variable for it, so only a libcurl program could measure it): set on every performed transfer, failed connects included, since the size is declared by the options, not measured on the wire.
  - `CURLINFO_COOKIELIST`: `CookieEngine.ListLines` - every cookie in the engine's shared store as a Netscape line (`NetscapeCookieFile.FormatLine`), in stored order, while a cookie file or jar turns the cookie engine on; empty for `CURLOPT_COOKIE` alone, as libcurl keeps no cookie list then.
- Left at defaults, with follow-ups:
  - `CURLINFO_CONN_ID`: already the work of BL-2362 (Backlog), which numbers `conn_id` through `ManagedEasyTransfer` per pooled connection.
  - `CURLINFO_SSL_VERIFYRESULT`, `PROXY_SSL_VERIFYRESULT`, `OS_ERRNO`, `PROXY_ERROR`: `TransferReport` carries none of them; filed BL-2366 (Protocol.Abstractions, Networking, Engine).
  - `CURLINFO_PROXYAUTH_USED` and the RTSP four: no proxy-side pick nor RTSP session/CSeq on the report; filed BL-2367 (Abstractions, Http, Rtsp, Engine).
  - `CURLINFO_CERTINFO`: needs `CURLOPT_CERTINFO` on `Curl.Api`'s `EasyTlsOptions` first; filed BL-2368.
  - `CURLINFO_CAINFO`, `CAPATH`, `SSL_ENGINES`, `TLS_SSL_PTR` off Windows (defaults live in `Curl.Api`'s `TransferInfo`, outside this task's touches), and `LASTSOCKET`, `ACTIVESOCKET` (the engine owns no OS socket descriptor) and `EARLYDATA_SENT_T` (Curl sends no TLS early data, so 0 is right today): filed BL-2369.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reported 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedEasyTransfer.Info now fills CURLINFO_RETRY_AFTER, CONTENT_LENGTH_UPLOAD and COOKIELIST; the rest are filed as BL-2366 to BL-2369 (conn_id is BL-2362)
