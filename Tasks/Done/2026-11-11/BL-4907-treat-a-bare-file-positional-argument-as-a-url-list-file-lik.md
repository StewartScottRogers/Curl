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
completed: 2026-11-11
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

- [x] `curl -s @urls.txt; echo $?` prints `0` and leaves `u1.txt` and `u2.txt`.
- [x] `curl -s @missing.txt` prints REF's two-line message and exits 26.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 11): `CommandLineParser.AddPositionalUrl` now applies a non-option argument that starts with `@` through the `--url` row (`CommandLineOptionTable.TryFindLong("url")`), so it goes through `AddUrlsFromFile` with the argument as typed as the spelled option. Other positional URLs go through `options.AddUrl` as before. This also covers `@file` after `--`, because curl 8.22.0 hands that to `--url`'s parser too. I measured real curl 8.22.0 Schannel here: `curl -s @missing.txt` and `curl -s -- @missing.txt` both print `curl: option @missing.txt: error encountered when reading a file` plus the try line, and exit 26. This matches measured curl, so no ADR was needed. Tests: `Curl.Cli.UnitTests/PositionalUrlListFileTests.cs` (3 tests).
- End to end, the built Curl against a PowerShell HttpListener stand-in: `curl -s @urls.txt` exits 0 and saves `u1.txt` and `u2.txt` with the right bodies. Real curl gives exit 0 against the same server. `@missing.txt` prints curl's two lines and exits 26.
- Environment: this machine no longer has SDK 10.0.401, which `global.json` pins (installed: 10.0.100-rc.1, 11.0.100). I built and tested by running `dotnet` from `%TEMP%`, so SDK 11.0.100 built the net10.0 projects: build clean, 0 warnings, Curl.Cli.UnitTests 4510 passed. `dotnet format` could not run, because it resolves the SDK from `global.json`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` ran every test project that reaches the library. All passed except two SSPI NTLM tests in Curl.Console.UnitTests: this Windows build's SSPI sends other Type 1 flags, unrelated to this change, filed as BL-4960. I measured from those coverage files with `-SkipTestRun`. Curl.Cli.UnitLibrary has 100% line coverage, and nothing in `CommandLineParser.cs` fails a gate. Two members in files this task does not touch fail on branch coverage: `WrappedMessage.LastBlankAtOrBefore` and `FormPartParameterReader.IsBlank`. They may come from SDK 11 code generation, and CI's SDK 10 run will show it.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a1a25fc3 cb17d305 a0b49a29.
- 2026-11-11: Doing -> Done. A bare @file positional argument is read as a URL-list file like --url @file
