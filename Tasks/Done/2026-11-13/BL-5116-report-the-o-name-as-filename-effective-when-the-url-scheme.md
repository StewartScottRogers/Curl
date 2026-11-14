---
id: BL-5116
title: Report the -o name as %{filename_effective} when the URL scheme is unsupported
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/FilenameEffectiveOnUnsupportedSchemeTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5116 — Report the -o name as %{filename_effective} when the URL scheme is unsupported

## Goal

`%{filename_effective}` (and `%{json}` `filename_effective`) reports the `-o` name even when the transfer fails because the URL scheme is unsupported (exit 1), as upstream curl does.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-066 (Low, Confirmed twice; the Linux OpenSSL build agrees with Windows curl). Area: `-w %{filename_effective}` / `%{json}`.

**Repro.** `curl -s -o x.out -w "[%{filename_effective}]\n" nosuch://x; echo $?` and `curl -s -o NUL -w "%{json}" nosuch://x`

**Expected (upstream curl).** `[x.out]`, exit 1; JSON `"filename_effective":"NUL"`.

**Actual.** `[]`, exit 1; JSON `"filename_effective":null`. The exit 7 and exit 3 cases already match.

**Where to start.** `Curl.Console/CurlCommandRunner.cs`: `Running.OutputFileName` is assigned (about lines 3127 and 3202) only after the protocol check passes. Assign it before the unsupported-scheme refusal.

## Acceptance criteria

- [x] New tests in `Curl.Console.UnitTests/FilenameEffectiveOnUnsupportedSchemeTests.cs` prove that `-o x.out -w "[%{filename_effective}]" nosuch://x` writes `[x.out]` and exits 1, and that `%{json}` for `-o NUL` carries `"filename_effective":"NUL"`.
- [x] Regression tests prove the exit 7 and exit 3 cases keep their present `filename_effective` output, and that no file `x.out` is created by the unsupported-scheme failure.
- [x] Tests are platform-neutral (no drive-letter paths).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the first repro command prints `[x.out]` and exits 1.

- Fix: `TransferWithBodyAsync` now names the -o file (`NameOutputFileOfRefusedScheme`) when the refusal is exit 1. Exit 3 keeps `[]`; a failed supported-scheme transfer (52 with an empty script) keeps its name. Tests are platform-neutral; CI on Linux and macOS is covered by that. Quality run for Curl.Console: 0 failing members.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: d5830f5d ff3382cc 949a0f54 a0b49a29.
- 2026-11-13: Doing -> Done. -o name now reported as filename_effective on unsupported scheme; tests and quality run green
