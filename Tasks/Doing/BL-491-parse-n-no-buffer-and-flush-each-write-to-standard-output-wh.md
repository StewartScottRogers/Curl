---
id: BL-491
title: Parse -N/--no-buffer and flush each write to standard output while it is set
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-491 — Parse -N/--no-buffer and flush each write to standard output while it is set

## Goal

`-N`, `--no-buffer` and `--buffer` are accepted, and while buffering is off every block of body bytes the handler writes reaches the output stream at once (flushed per write) instead of waiting in a buffer, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 3 (Blocker): Curl refuses `-N` with exit 2.
- The alias-table row is `buffer` with letter `N` and a documented `--no-` form (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`): `-N` means `--no-buffer`, and `--buffer` turns buffering back on.
- The output streams are opened in `Curl.Console` (`StandardOutputOpener.cs`, `OutputFileTarget.cs`, `DeferredOutputFileStream.cs`); find where standard output is buffered and add a flush-per-write wrapper only when buffering is off.
- `-N` is a common streaming idiom (`curl -N https://.../events`), which is why the audit ranks it a Blocker.

## Acceptance criteria

- [ ] `-N`, `--no-buffer` and `--buffer` parse (last one wins) into a named `CommandLineOptions` property; `Curl.Cli.UnitTests` covers each spelling and the bundle `-sN`.
- [ ] A `Curl.Console.UnitTests` test with a fake handler that writes two blocks and a recording output stream shows each block flushed before the next is written with `-N`, and not flushed per write without it.
- [ ] Standard output bytes are unchanged by `-N` (same bytes, same order) for a loopback-style fake transfer.
- [ ] New tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and `-Library Curl.Console` report 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
