---
id: BL-3187
title: The -u password prompt matches curl 8.22.0's text and newline (BL-3171 item 5)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs, Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3187 — The -u password prompt matches curl 8.22.0's text and newline (BL-3171 item 5)

## Goal

`-u user` with no password prompts with curl 8.22.0's exact text and writes its newline after the entry.

## Context

BL-3171 item 5. `Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs` and the prompt text `Enter host password for user '<user>':` (in `CommandLineOptions.cs`, which BL-3183 holds: change it only after BL-3183 is Done or if the text there is already right). Measure the exact bytes on Windows; take the non-Windows terminal behaviour from upstream's 8.22.0 source.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [x] A test in `Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs` (extend) pins the prompt text and the newline written after the entry to curl 8.22.0's bytes, for the Windows and non-Windows terminals where they differ.
- [x] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

- Measured curl 8.22.0 (x86_64-w64-mingw32, Schannel) with Record-CurlExchange.ps1 -NoServer. `-u,bob,bogus://x` blocks in `_getch` on the console, which the recorder cannot feed keys to, so it was stopped. The run on `-u bob bogus://x` showed curl's stderr is text mode: each `"...\n"` error line came out as `0D 0A`. Upstream 8.22.0 `tool_getpass.c` writes `fputs("\n", tool_stderr)` after the entry (always on Windows; on POSIX only when echo was turned off), so the bytes are CR LF on Windows and LF on a POSIX terminal. The prompt text is `Enter %s password for user '%s':` (`tool_paramhlp.c` `checkpasswd`).
- `ConsolePasswordPrompt` already writes `TextWriter.NewLine` (Environment.NewLine on Console.Error), and `CommandLineOptions.cs` already has the right text (pinned by CommandLinePasswordPromptTests), so no production change and BL-3183's file is untouched. Added two OSCondition tests pinning the literal bytes: `...':\r\n` on Windows and `...':\n` off Windows. CI off Windows is covered by keeping the tests platform-split; fast tests green here (4129 passed).
- Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members.
- Filed BL-3190: off Windows, curl reads /dev/tty and writes no LF when echo was not turned off (redirected input, no terminal). The port always writes it.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 38870c22 6361b4ee.
- 2026-10-22: Doing -> Done. Tests pin the -u password prompt's text and the CR LF (Windows) / LF (POSIX) after the entry to curl 8.22.0's bytes
