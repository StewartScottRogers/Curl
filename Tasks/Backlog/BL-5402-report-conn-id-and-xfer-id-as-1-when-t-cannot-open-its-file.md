---
id: BL-5402
title: Report %{conn_id} and %{xfer_id} as -1 when -T cannot open its file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5402 — Report %{conn_id} and %{xfer_id} as -1 when -T cannot open its file

## Goal

When `-T` cannot open its file, `%{conn_id}` and `%{xfer_id}` are `-1` (and `%{json}` has `"conn_id":-1,"xfer_id":-1`), as curl reports, with exit 26 unchanged.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-110 (Low, Confirmed, 2 runs). Area: `-T` early failure, write-out, Windows.

**Repro.** In a directory without `nofile.txt`, with any HTTP server on 127.0.0.1:41000:
`curl -s -T nofile.txt -o NUL -w '%{conn_id} %{xfer_id}\n' http://127.0.0.1:41000/echo/`
**Expected (REF).** `-1 -1`, exit 26; `%{json}` has `"conn_id":-1,"xfer_id":-1`.
**Actual (PORT).** `0 0`, exit 26.
**Where to start.** `Curl.Console/CurlCommandRunner.cs`, the `-T` open-failure write-out path (commit e689d0d0; the result of a transfer whose `-T` file could not be opened, near line 655).

## Acceptance criteria

- [ ] New tests in `Curl.Console.UnitTests` (a new file) prove `-T <missing file> -w '%{conn_id} %{xfer_id}\n'` prints `-1 -1` with exit 26, and `-w '%{json}'` contains `"conn_id":-1,"xfer_id":-1`.
- [ ] A test proves a `-T` upload that opens its file keeps its normal `conn_id` and `xfer_id` (no regression).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [ ] Hand check recorded in the Log: the repro prints `-1 -1`. Measure real curl with `Record-CurlExchange.ps1` where possible.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints `-1 -1`.

## Log

- 2026-11-17: Created.
