---
id: BL-5038
title: Split -H @file header lines on LF only, trimming one trailing CR
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/FormPartParameterReader.cs, Curl.Cli.UnitTests/CommandLineHeaderFileSplitTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5038 — Split -H @file header lines on LF only, trimming one trailing CR

## Goal

`-H @file` splits the file into header lines on LF only and strips at most one trailing CR per line, so a lone CR inside a line is sent as part of that line.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-043 (Low, Confirmed).

- **Repro:** a server that echoes the raw request head as its body; `printf 'X-A: a\rX-B: b\n' > hf; curl -s -H @hf http://127.0.0.1:41000/echo | od -c`
- **Expected (REF and REF2):** the request contains `X-A: a\rX-B: b\r\n` (one header line); curl splits a header file on LF only and strips one trailing CR per line.
- **Actual (PORT):** the request contains `X-A: a\r\nX-B: b\r\n`, two headers.
- **Where to start:** `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` line 1100, `.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)`: split on `'\n'` only and trim one trailing `'\r'`. Check `FormPartParameterReader.AddHeaderFileLines` (`-F ...;headers=@file`) the same way.

## Acceptance criteria

- [x] A new `Curl.Cli.UnitTests/CommandLineHeaderFileSplitTests.cs` asserts that a header file holding `X-A: a\rX-B: b\n` yields one header `X-A: a\rX-B: b`.
- [x] A file with `A: 1\r\nB: 2\r\n` (CRLF) yields exactly `A: 1` and `B: 2`; blank lines are still skipped as before.
- [x] A matching test in `Curl.Cli.UnitTests` for `-F ...;headers=@file` through `FormPartParameterReader.AddHeaderFileLines` gives the same results.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Check blank-line handling against real curl before pinning it; the review only covers the lone-CR case.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 3e7486f5 13becb13 d3a31470 a0b49a29.
- 2026-11-12: `-F ...;headers=@file` (AddHeaderFileLines) already split on LF only; pinned by new tests. Fixed `-H @file` split; extracted helper to keep AddHeaderValue within complexity 10. Cli quality run clean. Platform-neutral tests.
