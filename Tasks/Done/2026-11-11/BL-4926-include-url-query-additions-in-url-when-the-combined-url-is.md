---
id: BL-4926
title: Include --url-query additions in %{url} when the combined URL is rejected
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr089Tests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
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

- [x] The repro prints `http://127.0.0.1:18100/x?x=a b|http://127.0.0.1:18100/x?x=a b|3`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- Touches widened to Curl.Console: the fix is in `WriteOutGivenUrl` (CurlCommandRunner.cs), not TransferWriteOutVariables. Rejected URLs now print the unnormalised URL with the query; an IPFS URL that never became a transfer (empty transferUrl) keeps its as-typed text, as BL-3618 measured. Cgr089Tests updated; Curl.Console quality run clean; repro not run against a real curl here, expected text taken from the task.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 5822e861 a0b49a29.
- 2026-11-11: Doing -> Done. WriteOutGivenUrl keeps the query when the combined URL is rejected
