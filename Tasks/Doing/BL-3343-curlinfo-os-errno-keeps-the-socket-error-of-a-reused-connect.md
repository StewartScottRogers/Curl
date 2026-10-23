---
id: BL-3343
title: CURLINFO_OS_ERRNO keeps the socket error of a reused connection that died before its response when the retry on a fresh connection succeeds
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ReusedConnectionOsErrno.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3343 — CURLINFO_OS_ERRNO keeps the socket error of a reused connection that died before its response when the retry on a fresh connection succeeds

## Goal

When a reused HTTP/1.1 connection the server closed while idle fails its receive (WSAECONNRESET 10054 on Windows) and the request is resent on a fresh connection that succeeds, the transfer's CURLINFO_OS_ERRNO is that receive's socket error, as libcurl 8.22.0 reports.

## Context

Found by BL-3315's differential run (LibcurlEasyDifferentialTests, case reuse-idle-close: round 1 GETs `/idle-close`, whose server closes the kept connection after a pause; round 2 GETs `/echo` on the same handle): libcurl reports `200019=0:10054`, Curl `0`. BL-3329 put a failed receive's `SocketException.NativeErrorCode` on `HttpTransferException.OsErrno` and copies it to the report only when the transfer fails (`HttpProtocolHandler.WithHeadBeforeFailure`). The retry path that gives up a dead pooled connection (`SettleConnection`, `outcome.DiedBeforeResponse`, "Connection died, retrying a fresh connect", around HttpProtocolHandler.cs line 1100) drops it: carry the dead attempt's socket error to the fresh attempt's `TransferReport.OsErrno` unless that attempt has its own. HttpProtocolHandler.cs is also in BL-3320's touches, so this waits until that lands.

## Acceptance criteria

- [ ] A unit test in Curl.Protocol.Http.UnitTests (a reused connection whose receive throws a SocketException with ConnectionReset, then a fresh connection that answers 200) pins `TransferReport.OsErrno` as that exception's `NativeErrorCode`, platform-neutral.
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for Curl.Protocol.Http.UnitLibrary.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: cb02d211 74c4d232.
