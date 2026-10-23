---
id: BL-3180
title: Wrap missing-file and @file refusals at the parse's COLUMNS, not the process's
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/CommandLineRefusalWrappingTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3180 — Wrap missing-file and @file refusals at the parse's COLUMNS, not the process's

## Goal

The `--cacert`/`--proxy-cacert`/`--knownhosts`/`--netrc-file` missing-file refusal and the `-d @file` / `-H @file` unreadable-file refusal wrap at the width the parse's own environment gives (`options.ReadEnvironmentVariable("COLUMNS")`), as the `-K` refusals already do.

## Context

BL-3169 made `CommandLineRefusal.FileDoesNotExist` and `CommandLineRefusal.DataFileUnreadable` wrap their first line as curl 8.22.0 does, with an optional `int? terminalColumns` that falls back to this process's `COLUMNS`. Their callers in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` (`SettingExistingFile`, near line 1136, and the `@file` reader near line 1042) were outside BL-3169's touches, so they pass nothing and a parse given its own environment (Curl.Api, Libcurl) wraps at the process's `COLUMNS`. Pass `WrappedMessage.TerminalColumns(options.ReadEnvironmentVariable("COLUMNS"))` there, as `ConfigFileApplier.cs` does.

## Acceptance criteria

- [ ] Both callers in `CommandLineOptionTable.cs` pass `WrappedMessage.TerminalColumns(options.ReadEnvironmentVariable("COLUMNS"))`.
- [ ] A test in `Curl.Cli.UnitTests` parses `--cacert` with a long missing path and an environment whose `COLUMNS` is 40, and gets the 40-column lines BL-3169 recorded.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 0c02f190 83e1c879 ae25be8d.
