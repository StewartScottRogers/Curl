---
id: BL-3199
title: Keep header bytes out of the write callback under CURLOPT_NOBODY
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitLibrary/CallbackWriteStream.cs, Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/NobodyWriteCallbackTests.cs, Libcurl.UnitTests/Easy/CurlEasyNobodyTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3199 — Keep header bytes out of the write callback under CURLOPT_NOBODY

## Goal

With `CURLOPT_NOBODY` set, the body (write) callback receives zero bytes; headers go only to the header callback, or nowhere the body callback can see, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 19). Repro: set Nobody=1, a write callback and a header callback, perform any GET. Upstream delivers headers only to the header callback; ours also hands the header bytes to the body callback. This hit about 77 of Stewart's cases, so it is the highest priority of this batch. Without a header callback upstream still sends nothing to the write callback unless `CURLOPT_HEADER` is set; measure that too. Start at the engine's write path (`CallbackWriteStream.cs`, `HeaderLineTeeStream.cs`) and where `ManagedEasyTransfer` wires Nobody.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/NobodyWriteCallbackTests.cs` shows a HEAD-style (Nobody=1) transfer gives zero body-callback bytes over HTTP and over FTP, each with and without a header callback.
- [x] A test shows `CURLOPT_HEADER`=1 with Nobody=1 behaves as upstream measured (headers to the write callback only then).
- [x] `Libcurl.UnitTests/Easy/CurlEasyNobodyTests.cs` covers the same through `CurlEasy` with the public options.
- [x] `dotnet build -warnaserror` is clean for the changed projects and their fast tests pass; Measure-CodeQuality.ps1 reports 0 failing members for them.

## Notes

- Cause: `TransferContextFactory.HeaderOutputOf` treated `EasyTransferOptions.NoBody` like the tool's `-I`
  and sent header lines to the body output. The tool's `-I` already sets `ShowHeaders` (`IncludeHeader`), so
  the fix keys on `CURLOPT_HEADER` alone, except for FTP (ADR-0777). Added
  `Curl.Engine.UnitLibrary/TransferContextFactory.cs` to touches: the fix lives there, and no task in Doing on
  `origin/work/dark-factory` names it. `CallbackWriteStream`, `HeaderLineTeeStream` and `ManagedEasyTransfer`
  needed no change.
- Measured on curl 8.22.0 (Windows) with `-I --no-include` (NOBODY 1, HEADER 0): HTTP stdout empty (`-I` alone
  gives the head); `file://` empty; FTP file URL stdout `Last-Modified`/`Content-Length`/`Accept-ranges`
  (upstream `ftp.c` `client_write_header` forces include for them); FTP directory URL empty. Measured with
  `Record-CurlExchange.ps1` for HTTP and a throwaway PowerShell loopback FTP responder for FTP.
- Criterion 1: the FTP case with zero body bytes is the directory URL; for a file URL, upstream itself writes
  the `-I` info lines to the write callback, so the tests pin upstream's lines there rather than zero
  (matching platform curl wins, ADR-0777). The header callback gets FTP's replies with those lines between them,
  as `-D` showed.
- Results are platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS should match. Engine
  tests 2255 passed, Libcurl 1340 passed, full fast run green apart from one assertion in the new test, since
  fixed; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 7182a958 6cf60054 417fc3f2 aaa5b3d3.
- 2026-10-22: Doing -> Done. A CURLOPT_NOBODY handle's write callback gets no header bytes over HTTP; FTP's -I lines follow curl 8.22.0
