---
id: BL-3199
title: Keep header bytes out of the write callback under CURLOPT_NOBODY
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CallbackWriteStream.cs, Curl.Engine.UnitLibrary/HeaderLineTeeStream.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/NobodyWriteCallbackTests.cs, Libcurl.UnitTests/Easy/CurlEasyNobodyTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3199 — Keep header bytes out of the write callback under CURLOPT_NOBODY

## Goal

With `CURLOPT_NOBODY` set, the body (write) callback receives zero bytes; headers go only to the header callback, or nowhere the body callback can see, as upstream does.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 19). Repro: set Nobody=1, a write callback and a header callback, perform any GET. Upstream delivers headers only to the header callback; ours also hands the header bytes to the body callback. This hit about 77 of Stewart's cases, so it is the highest priority of this batch. Without a header callback upstream still sends nothing to the write callback unless `CURLOPT_HEADER` is set; measure that too. Start at the engine's write path (`CallbackWriteStream.cs`, `HeaderLineTeeStream.cs`) and where `ManagedEasyTransfer` wires Nobody.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/NobodyWriteCallbackTests.cs` shows a HEAD-style (Nobody=1) transfer gives zero body-callback bytes over HTTP and over FTP, each with and without a header callback.
- [ ] A test shows `CURLOPT_HEADER`=1 with Nobody=1 behaves as upstream measured (headers to the write callback only then).
- [ ] `Libcurl.UnitTests/Easy/CurlEasyNobodyTests.cs` covers the same through `CurlEasy` with the public options.
- [ ] `dotnet build -warnaserror` is clean for the changed projects and their fast tests pass; Measure-CodeQuality.ps1 reports 0 failing members for them.

## Notes

## Log

- 2026-10-22: Created.
