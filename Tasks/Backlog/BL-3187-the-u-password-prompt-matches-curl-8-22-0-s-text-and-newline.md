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
completed:
---
# BL-3187 — The -u password prompt matches curl 8.22.0's text and newline (BL-3171 item 5)

## Goal

`-u user` with no password prompts with curl 8.22.0's exact text and writes its newline after the entry.

## Context

BL-3171 item 5. `Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs` and the prompt text `Enter host password for user '<user>':` (in `CommandLineOptions.cs`, which BL-3183 holds: change it only after BL-3183 is Done or if the text there is already right). Measure the exact bytes on Windows; take the non-Windows terminal behaviour from upstream's 8.22.0 source.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs` (extend) pins the prompt text and the newline written after the entry to curl 8.22.0's bytes, for the Windows and non-Windows terminals where they differ.
- [ ] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

## Log

- 2026-10-22: Created.
