---
id: BL-2317
title: Fill TransferInfo from the engine's transfer report for every CURLINFO_* the engine measures
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2317 — Fill TransferInfo from the engine's transfer report for every CURLINFO_* the engine measures

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info` holds every `CURLINFO_*` value the engine measures (response code, effective URL and method, times, sizes, speeds, IPs and ports, redirect count and URL, auth used and available, connection and transfer IDs, TLS verify result, certificates and the rest), taken from the transfer's `TransferReport`.

## Context

ADR-0506, plan phase 2 step 4; needs BL-2316. `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` shows what Curl measures today for `-w`; every value it can print has a `TransferInfo` property to fill. A `CURLINFO_*` the engine does not measure yet keeps its fresh-handle default; list each such one in Notes and file one follow-up task for them.

## Acceptance criteria

- [x] A mapping class in `Curl.Engine.UnitLibrary` (for example `TransferInfoFromReport`) fills `TransferInfo` from `TransferReport`, and `ManagedEasyTransfer.Info` returns it after each perform.
- [x] `TransferInfoFromReportTests` cover every property the mapping sets, one assertion each.
- [x] This task's Notes list every `CURLINFO_*` left at its default, and a follow-up task is filed for them (its ID in Notes).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.

## Notes

- `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs` (`Of(result, requestUrl, fileTimeRequested, timeProvider)`) fills `TransferInfo`; `ManagedEasyTransfer.PerformAsync` sets `Info` from it after each perform. Each value is worked out as `TransferWriteOutVariables` works out its `-w` twin, so `getinfo` and `-w` agree.
- Filled: EFFECTIVE_URL, EFFECTIVE_METHOD, REDIRECT_URL, CONTENT_TYPE, FTP_ENTRY_PATH, REFERER, RESPONSE_CODE, HTTP_CONNECTCODE, HTTP_VERSION, REDIRECT_COUNT, HEADER_SIZE, REQUEST_SIZE, NUM_CONNECTS, CONDITION_UNMET, USED_PROXY, HTTPAUTH_USED, HTTPAUTH_AVAIL, PROXYAUTH_AVAIL, CONTENT_LENGTH_DOWNLOAD, PRIMARY_IP, PRIMARY_PORT, LOCAL_IP, LOCAL_PORT, SIZE_DOWNLOAD, SIZE_UPLOAD, SPEED_DOWNLOAD, SPEED_UPLOAD, the nine times (TOTAL, QUEUE, NAMELOOKUP, CONNECT, APPCONNECT, PRETRANSFER, POSTTRANSFER, STARTTRANSFER, REDIRECT), SCHEME, PROTOCOL, FILETIME.
- Choices (defaults taken): SCHEME and PROTOCOL come from the effective URL and are set only when a handler ran (the result has a report), as libcurl sets them at connect. FILETIME is -1 unless `EasyTransferOptions.FileTime` (CURLOPT_FILETIME) is set, as libcurl. HTTPAUTH_AVAIL reads a 401's `WWW-Authenticate` lines and PROXYAUTH_AVAIL a 407's `Proxy-Authenticate` lines (the scheme word of each), as libcurl's `Curl_http_input_auth` is called only for those codes; a CONNECT's 407 is not in the report, so a tunnel proxy's offer stays 0. CONTENT_LENGTH_DOWNLOAD is the last response's `Content-Length`, -1 when absent or unparsable. QUEUE_TIME is 1 microsecond when timed, as `%{time_queue}` prints.
- Left at defaults (no engine measurement yet), filed as BL-2361: XFER_ID, CONN_ID (numbered tool-side), SSL_VERIFYRESULT, PROXY_SSL_VERIFYRESULT, OS_ERRNO, PROXY_ERROR, PROXYAUTH_USED (the report has no proxy-side pick), RETRY_AFTER, CONTENT_LENGTH_UPLOAD, EARLYDATA_SENT_T, LASTSOCKET, ACTIVESOCKET, COOKIELIST, CERTINFO (no CURLOPT_CERTINFO option yet), RTSP_SESSION_ID, RTSP_CLIENT_CSEQ, RTSP_SERVER_CSEQ, RTSP_CSEQ_RECV. CAINFO, CAPATH, SSL_ENGINES and TLS_SSL_PTR are build constants whose defaults are the Schannel build's answer (BL-2361 covers the OpenSSL build's).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reported 0 failing members. `PassingUpstreamCases.txt` untouched; the conformance cases run in the fast test pass.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedEasyTransfer.Info now holds every CURLINFO_* the transfer report measures, through TransferInfoFromReport; the rest are filed as BL-2361
