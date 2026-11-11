---
id: BL-4908
title: Make -o - write to stdout under --remote-name-all
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/RemoteNameAllStdoutOutputTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4908 — Make -o - write to stdout under --remote-name-all

## Goal

`--remote-name-all -o - URL` writes the body to stdout, as curl does.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-004 (Medium, confirmed: reproduced at least twice).

- Repro: Server where `GET /a` returns `hello /a\n`. In an empty directory: `curl -s --remote-name-all -o - http://127.0.0.1:18100/a; ls` (and with `-o -` after the URL).
- Expected: REF 8.22.0: stdout `hello /a`, no file `a`. With two URLs (`-o - URL/a URL/b`): stdout `hello /a` plus file `b`.
- Actual: Stdout empty, body saved to `a`; with two URLs files `a` and `b`.
- Where to start: `CommandLineOptions.PairOutputFile` (~line 2368) sets `FileName` but leaves `UsesRemoteName = true`; `Curl.Console/CurlCommandRunner.cs` then treats `-` as no file. `PairOutputFile` should clear `UsesRemoteName`.

## Acceptance criteria

- [ ] `curl -s --remote-name-all -o - http://127.0.0.1:18100/a` prints `hello /a` and leaves no file `a`.
- [ ] The two-URL form prints `hello /a` and saves only `b`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 6b278360 96a45974 a0b49a29.
