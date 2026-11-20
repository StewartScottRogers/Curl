---
id: BL-5603
title: Give Curl.Api an FTP file-time option, so a HEAD on an ftp URL asks MDTM and reports the time
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlFtpOptions.cs, Curl.Api/Client/CurlClient.cs, Curl.Api/Client/CurlClientOptions.cs, Curl.Api/README.md, Curl.Api.UnitTests/Client/CurlClientTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5603 — Give Curl.Api an FTP file-time option, so a HEAD on an ftp URL asks MDTM and reports the time

## Goal

A `Curl.Api` user can ask for the remote file time (`CURLOPT_FILETIME`) on an FTP request; a `HEAD` then sends `MDTM` and the response reports the time, as `curl -I -R` and libcurl's `CURLINFO_FILETIME` do.

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #5. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`Curl.Api` has no FILETIME option; a `HEAD` on an FTP URL sends `SIZE` only, so the date needs `MDTM` through `Quote`. The engine and `Libcurl` already have file-time support (`EasyTransferOptions`, `TransferInfo`); expose it in `Curl.Api`'s option groups (for example `CurlFtpOptions.FetchFileTime`, or a transfer-wide option, since curl's applies to HTTP and FTP alike) and the time on the response. Document it in the README.

## Acceptance criteria

- [x] A `Curl.Api` `HEAD` to an FTP test server with the new option sends `MDTM` and the response carries the file's time; without it only `SIZE` is sent, as now.
- [x] The same option on an HTTP request reports `Last-Modified` as the file time, as libcurl does.
- [x] Focused unit tests in `Curl.Api.UnitTests` pin each case; the README names the option; `dotnet build` is clean and the fast tests pass.

## Notes

Decision: the option is transfer-wide, `CurlClientOptions.FetchFileTime` (CURLOPT_FILETIME applies to HTTP and FTP alike), applied in `CurlClient.CreateEasy`; not on CurlFtpOptions, and not per request because another task holds CurlRequestOptions.cs. touches widened (none held by another Doing task). The engine already sends MDTM under RemoteTime (FtpSession, upstream test542) and reports TransferInfo.FileTime from Last-Modified for HTTP, each with its own engine tests; Curl.Api tests pin that the option reaches Transfer.FileTime. Measure-CodeQuality for Curl.Api: 0 failing.

## Log

- 2026-11-20: Created from Surly bug reports (2203 #5).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 03bec8a6 020748a8 80781fb3.
- 2026-11-20: Doing -> Done. FetchFileTime on CurlClientOptions asks MDTM/Last-Modified file time
