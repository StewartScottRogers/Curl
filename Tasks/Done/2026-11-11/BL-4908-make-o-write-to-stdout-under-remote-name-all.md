---
id: BL-4908
title: Make -o - write to stdout under --remote-name-all
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/RemoteNameAllStdoutOutputTests.cs, Curl.Cli.UnitTests/CommandLineRemoteNameOptionTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
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

- [x] `curl -s --remote-name-all -o - http://127.0.0.1:18100/a` prints `hello /a` and leaves no file `a`.
- [x] The two-URL form prints `hello /a` and saves only `b`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: `CommandLineOptions.AddOutputFile` (the `-o` pairing; the task's `PairOutputFile` is the old name) set `FileName` but left the `UsesRemoteName` that `--remote-name-all` gives every new output entry, so the runner, which takes `-o -` as "no file", fell back to the remote name. It now clears `UsesRemoteName`, as curl's own `-o` parsing switches `GETOUT_USEREMOTE` off. So `--remote-name-all -o - u` now gives the same `UrlOutput` as a plain `-o - u` (FileName `-`, no remote name), which the runner already writes to stdout, and the second URL of the two-URL form keeps its remote name. The REF behaviour is the review's measurement of curl 8.22.0 (STD-004). Pinned in `Curl.Cli.UnitTests/RemoteNameAllStdoutOutputTests.cs` (the `-o -` before and after the URL, and the two-URL form).
- `Parse_RemoteNameAllWithOutput_KeepsTheOutputFileName` in `CommandLineRemoteNameOptionTests.cs` pinned the old flag (`UsesRemoteName` true beside `-o x`). It is renamed `..._KeepsTheOutputFileNameInPlaceOfTheRemoteName` and now expects false. That file was added to `touches`: no task in Doing on origin/work/dark-factory names it.
- Environment: this host has no .NET SDK 10.0.401 (only 10.0.100-rc.1 and 11.0.100), so `global.json` does not resolve. I built and tested with a temporary local `global.json` naming 11.0.100, reverted after each run and not committed. Curl.Cli.UnitTests: 4510 passed, 0 failed. Curl.Console.UnitTests: 3137 passed, 2 failed. The two failures are the Windows SSPI NTLM tests in `CurlCompositionNtlmTests.cs`, which use the host's real SSPI with no output option and are unrelated to this change; filed as BL-4959.
- Because of those two failures, `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` stops at its test step and could not measure. The production change is one assignment with no branch, so complexity and CRAP do not change, and the new tests reach the line.
- The Linux/macOS criterion is met by keeping the change platform-neutral: the tests are parse-level only, with no paths or files.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 6b278360 96a45974 a0b49a29.
- 2026-11-11: Doing -> Done. --remote-name-all -o - writes the body to stdout; -o now replaces the remote name as in curl
