---
id: BL-2302
title: Define the TransferInfo read model for every CURLINFO_* in Curl.Api.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Info, Curl.Api.UnitTests/Info]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2302 — Define the TransferInfo read model for every CURLINFO_* in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/Info` (namespace `Curl.Api.Info`) defines `TransferInfo`, an immutable record with one property per `CURLINFO_*` in the plan's CURLINFO section, each documented with its `CURLINFO_*` name and the value libcurl reports before any transfer.

## Context

ADR-0506; plan section "CURLINFO". `curl_easy_getinfo` returns these; `Libcurl`'s `GetInfo(CurlInfo.X)` will read them, `Curl.Api` will expose them on its response, and `Curl.Console`'s `-w` reads them after BL-2319. Upstream: curl 8.22.0's `docs/libcurl/curl_easy_getinfo.md` and `docs/libcurl/opts/CURLINFO_*.md`. Times are `TimeSpan` (the `_T` microsecond variants and the double-seconds variants are both views of one property; `Libcurl` converts); sizes and speeds `long`; `CERTINFO` a list of certificates, each a list of `name:value` strings; `CURLINFO_PRIVATE` is not here (it is the caller's own value, which `Libcurl` keeps). The defaults are what `curl_easy_getinfo` answers on a fresh handle (0, -1 for `FILETIME`, `CONTENT_LENGTH_*` and `XFER_ID`/`CONN_ID`, `null` strings). `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` shows which values Curl measures today.

## Acceptance criteria

- [x] `TransferInfo` exists in `Curl.Api.UnitLibrary/Info` with a property for every `CURLINFO_*` in the plan's section except `PRIVATE`, each documented with its name.
- [x] Tests in `Curl.Api.UnitTests/Info` pin every fresh-handle default.
- [x] This task's Notes list each `CURLINFO_*` of the plan's section with the property that carries it.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Defaults decided by Claude under Stewart's delegation, from libcurl 8.22.0's `lib/getinfo.c` on a fresh handle (no ADR: each is libcurl's own answer, no choice among behaviours):
- `EFFECTIVE_URL` is `""`, not null (getinfo answers `""` when no URL is set); `PRIMARY_IP` and `LOCAL_IP` are `""` (fixed char arrays); `EFFECTIVE_METHOD` is `GET` (no custom request, no body flag, default `HTTPREQ_GET`). The other strings are null.
- `LASTSOCKET` and `ACTIVESOCKET` are -1 (no connection, `CURL_SOCKET_BAD`), beside the -1 the task named for `FILETIME`, `CONTENT_LENGTH_*`, `XFER_ID` and `CONN_ID`.
- `CAINFO`/`CAPATH` are null and `SSL_ENGINES` empty: the Schannel build's answer. `Libcurl` supplies the OpenSSL build's bundle off Windows; a record default that branched on the OS could not reach 100% branch coverage on the Windows quality job.
- `EARLYDATA_SENT_T` is a byte count (`long`), though the plan lists it with the times.
- `TLS_SSL_PTR` is `TlsSslBackend`, the `curl_sslbackend` number, default 8 (`CURLSSLBACKEND_SCHANNEL`); its `internals` pointer is always null, since there is no native TLS object.
- `RETRY_AFTER` is a `TimeSpan` (whole seconds in libcurl).

CURLINFO_* -> property (`_T` and double variants share one property):
EFFECTIVE_URL EffectiveUrl; EFFECTIVE_METHOD EffectiveMethod; REDIRECT_URL RedirectUrl; CONTENT_TYPE ContentType; PRIMARY_IP PrimaryIp; LOCAL_IP LocalIp; FTP_ENTRY_PATH FtpEntryPath; RTSP_SESSION_ID RtspSessionId; SCHEME Scheme; REFERER Referer; CAINFO CaInfo; CAPATH CaPath; PRIVATE (not here, kept by Libcurl);
RESPONSE_CODE ResponseCode; HTTP_CONNECTCODE HttpConnectCode; HTTP_VERSION HttpVersion; FILETIME(_T) FileTime; REDIRECT_COUNT RedirectCount; HEADER_SIZE HeaderSize; REQUEST_SIZE RequestSize; SSL_VERIFYRESULT SslVerifyResult; PROXY_SSL_VERIFYRESULT ProxySslVerifyResult; HTTPAUTH_AVAIL HttpAuthAvailable; PROXYAUTH_AVAIL ProxyAuthAvailable; HTTPAUTH_USED HttpAuthUsed; PROXYAUTH_USED ProxyAuthUsed; OS_ERRNO OsErrno; NUM_CONNECTS NumConnects; PRIMARY_PORT PrimaryPort; LOCAL_PORT LocalPort; CONDITION_UNMET ConditionUnmet; RTSP_CLIENT_CSEQ RtspClientCSeq; RTSP_SERVER_CSEQ RtspServerCSeq; RTSP_CSEQ_RECV RtspCSeqReceived; PROTOCOL Protocol; PROXY_ERROR ProxyError; USED_PROXY UsedProxy; XFER_ID TransferId; CONN_ID ConnectionId; LASTSOCKET LastSocket; ACTIVESOCKET ActiveSocket;
TOTAL_TIME TotalTime; NAMELOOKUP_TIME NameLookupTime; CONNECT_TIME ConnectTime; APPCONNECT_TIME AppConnectTime; PRETRANSFER_TIME PreTransferTime; POSTTRANSFER_TIME_T PostTransferTime; STARTTRANSFER_TIME StartTransferTime; REDIRECT_TIME RedirectTime; QUEUE_TIME_T QueueTime; EARLYDATA_SENT_T EarlyDataSent; RETRY_AFTER RetryAfter;
SIZE_UPLOAD SizeUpload; SIZE_DOWNLOAD SizeDownload; SPEED_UPLOAD SpeedUpload; SPEED_DOWNLOAD SpeedDownload; CONTENT_LENGTH_DOWNLOAD ContentLengthDownload; CONTENT_LENGTH_UPLOAD ContentLengthUpload;
SSL_ENGINES SslEngines; COOKIELIST CookieList; CERTINFO CertInfo; TLS_SSL_PTR TlsSslBackend.

Measured: Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferInfo defines every CURLINFO_* with libcurl's fresh-handle defaults; build, fast tests and quality gates green.
