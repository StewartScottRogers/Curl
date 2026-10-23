---
id: BL-3185
title: Bare curl prints upstream's try-help line without --ai-help, superseding ADR-0762 (BL-3171 item 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/CommandLineRefusalEmptyCommandLineTests.cs, Documentation/Planning/Decisions/ADR-0762-bare-curl-names-ai-help-in-its-try-help-line.md, Documentation/Planning/Decisions/README.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3185 — Bare curl prints upstream's try-help line without --ai-help, superseding ADR-0762 (BL-3171 item 3)

## Goal

`curl` with no arguments prints curl 8.22.0's exact try-help line (no `--ai-help`) and exits 2 on each platform; ADR-0762 is superseded.

## Context

BL-3171 item 3: reverses BL-3145. Undo `TryHelpLineNamingAiHelp` / `EmptyCommandLine` in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs`; keep the `--ai-help` hint only in `curl --help`'s footer. Mark ADR-0762 superseded citing Stewart's 2026-10-22 decision and update the Decisions README and the Glossary entry. curl 8.21.0 Schannel on Windows printed ONE line, `curl: try 'curl --help' for more information`, exit 2; measure 8.22.0 on Windows and pin non-Windows from upstream's source for 8.22.0 (the Windows build has no `--manual`, see BL-3169), each in its own `[OSCondition]` test. This task owns the try-help constant per platform.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [ ] Tests in `Curl.Cli.UnitTests/CommandLineRefusalEmptyCommandLineTests.cs` pin the no-argument standard error and exit 2 per platform (Windows, and non-Windows via `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) to curl 8.22.0's measured bytes, with no `--ai-help` in that line; `curl --help`'s footer still contains the `--ai-help` hint; `TryHelpLineNamingAiHelp` is gone; ADR-0762 is marked superseded citing Stewart's 2026-10-22 decision, the ADR index and Glossary agree, and `--ai-help` text still describes the options correctly.
- [ ] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: ba519adb 6361b4ee.
