---
id: BL-4926
title: Include --url-query additions in %{url} when the combined URL is rejected
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/UrlQueryRejectedWriteOutTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4926 — Include --url-query additions in %{url} when the combined URL is rejected

## Goal

`%{url}` and `%{url_effective}` agree when `--url-query` makes the URL invalid.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-016 (Low, confirmed: reproduced at least twice).

- Repro: `curl -s -w "%{url}|%{url_effective}|%{exitcode}\n" --url-query "+x=a b" http://127.0.0.1:18100/x` (no server).
- Expected: REF 8.22.0: `http://127.0.0.1:18100/x?x=a b|http://127.0.0.1:18100/x?x=a b|3`.
- Actual: `http://127.0.0.1:18100/x|http://127.0.0.1:18100/x?x=a b|3`.
- Where to start: `TransferWriteOutVariables.cs` line 154 receives the pre-`--url-query` URL on the rejected path.

## Acceptance criteria

- [ ] The repro prints `http://127.0.0.1:18100/x?x=a b|http://127.0.0.1:18100/x?x=a b|3`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
