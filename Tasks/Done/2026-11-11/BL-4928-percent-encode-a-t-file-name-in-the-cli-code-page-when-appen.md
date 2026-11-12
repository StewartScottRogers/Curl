---
id: BL-4928
title: Percent-encode a -T file name in the CLI code page when appending it to the URL
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/UploadUrl.cs, Curl.Cli.UnitTests/UploadUrlCodePageTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4928 — Percent-encode a -T file name in the CLI code page when appending it to the URL

## Goal

`-T` appends a non-ASCII file name with the same bytes the rest of the CLI uses (the ANSI code page on Windows).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-018 (Low, confirmed: reproduced at least twice).

- Repro: File `ünï.txt`; `curl -s -T ünï.txt -w "%{url_effective}\n" http://127.0.0.1:18200/up/` against a request-line logger (code page 1252).
- Expected: REF 8.22.0: `PUT /up/%FCn%EF.txt HTTP/1.1`; url_effective ends `/up/%FCn%EF.txt`.
- Actual: `PUT /up/%C3%BCn%C3%AF.txt HTTP/1.1`; `-F f=@ünï.txt`, `-d ü` and URL path `/ü` already use ANSI bytes.
- Where to start: `UploadUrl.cs` line 76: `Uri.EscapeDataString(BaseName(uploadFile))` always encodes UTF-8. Off Windows (UTF-8 locale) the result must stay UTF-8; pin each platform in its own test.

## Acceptance criteria

- [x] On Windows code page 1252 the request line is `PUT /up/%FCn%EF.txt HTTP/1.1` and the command prints `http://127.0.0.1:18200/up/%FCn%EF.txt`.
- [x] A Linux/macOS test pins the UTF-8 form.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

Notes: -T base name is encoded in the Windows ANSI code page (UTF-8 elsewhere) inside UploadUrl.EscapeBaseName; Windows pinned as %FCn%EF.txt, other platforms in their own test. Linux/macOS criterion met by platform-neutral tests. Quality run clean.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 5905ddb4 17652dbc a0b49a29.
- 2026-11-11: Doing -> Done. -T base name now percent-encoded in the ANSI code page on Windows, UTF-8 elsewhere
