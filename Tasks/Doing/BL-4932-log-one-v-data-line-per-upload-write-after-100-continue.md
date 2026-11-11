---
id: BL-4932
title: Log one -v data line per upload write, after 100 Continue
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-4909]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs, Curl.Original.Protocol.Http.UnitTests/UploadVerboseDataLinesTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4932 — Log one -v data line per upload write, after 100 Continue

## Goal

`-v` upload output matches curl: `< HTTP/1.1 100 Continue` first, then one `} [n bytes data]` per send, then `* upload completely sent off`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-022 (Low, confirmed: reproduced at least twice).

- Repro: Server that sends `100 Continue`, reads the body, replies 200. 2,000,000-byte `big.bin`. `curl -v -T big.bin -o NUL http://127.0.0.1:18200/a`; `echo hello | curl -v -T - -o NUL http://127.0.0.1:18200/a`.
- Expected: REF 8.22.0: `< HTTP/1.1 100 Continue`, `< `, 31 `} [65536 bytes data]` lines for 2 MB (also with `-H "Expect:"`; stdin: `} [10 bytes data]` then `} [5 bytes data]`), `* upload completely sent off: N bytes`, `< HTTP/1.1 200 OK`.
- Actual: A single `} [...]` line and `* upload completely sent off` before `< HTTP/1.1 100 Continue` (wire bytes correct).
- Where to start: `HttpRequestBodyWriter.cs` / `HttpExchangeLog.cs` log one line per body, and the order against response-line logging in `HttpContinueWaitConnection`.

## Acceptance criteria

- [ ] `curl -v -T big.bin -o NUL http://127.0.0.1:18200/a 2>&1 | findstr /b "}"` prints 31 lines and `< HTTP/1.1 100 Continue` precedes the first.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 53a58616 a0b49a29.
