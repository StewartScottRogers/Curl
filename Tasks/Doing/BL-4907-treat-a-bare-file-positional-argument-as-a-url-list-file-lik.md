---
id: BL-4907
title: Treat a bare @file positional argument as a URL-list file like --url @file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitTests/PositionalUrlListFileTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4907 — Treat a bare @file positional argument as a URL-list file like --url @file

## Goal

A non-option `@urls.txt` is handled exactly like `--url @urls.txt` (curl 8.13+).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-003 (Medium, confirmed: reproduced at least twice).

- Repro: HTTP server on 18100; `urls.txt` lists `http://127.0.0.1:18100/u1.txt` and `/u2.txt`. In an empty directory: `curl -s @urls.txt; echo "exit $?"; ls`
- Expected: REF 8.22.0: fetches both, saves `u1.txt` and `u2.txt` (implied `-O`), exit 0. Missing file: `curl: option @urls.txt: error encountered when reading a file` + the try line, exit 26.
- Actual: Treated as a literal URL: `curl: (6) Could not resolve host: urls.txt` (absolute Windows path: exit 3, backslash). `--url @urls.txt` works.
- Where to start: `CommandLineParser.AddPositionalUrl` (~line 404) calls `options.AddUrl(argument, argument)` directly; route it through `CommandLineOptionTable.AddUrl` (handles `@` via `AddUrlsFromFile`).

## Acceptance criteria

- [ ] `curl -s @urls.txt; echo $?` prints `0` and leaves `u1.txt` and `u2.txt`.
- [ ] `curl -s @missing.txt` prints REF's two-line message and exits 26.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a1a25fc3 cb17d305 a0b49a29.
