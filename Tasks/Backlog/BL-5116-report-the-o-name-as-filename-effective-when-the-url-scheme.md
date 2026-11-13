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
completed:
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

- [ ] New tests in `Curl.Console.UnitTests/FilenameEffectiveOnUnsupportedSchemeTests.cs` prove that `-o x.out -w "[%{filename_effective}]" nosuch://x` writes `[x.out]` and exits 1, and that `%{json}` for `-o NUL` carries `"filename_effective":"NUL"`.
- [ ] Regression tests prove the exit 7 and exit 3 cases keep their present `filename_effective` output, and that no file `x.out` is created by the unsupported-scheme failure.
- [ ] Tests are platform-neutral (no drive-letter paths).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the first repro command prints `[x.out]` and exits 1.

## Log

- 2026-11-13: Created.
