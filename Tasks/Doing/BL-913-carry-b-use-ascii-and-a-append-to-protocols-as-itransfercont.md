---
id: BL-913
title: Carry -B/--use-ascii and -a/--append to protocols as ITransferContext.UseAscii and Append
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-913 — Carry -B/--use-ascii and -a/--append to protocols as ITransferContext.UseAscii and Append

## Goal

`ITransferContext` gains `bool UseAscii` (`-B`/`--use-ascii`: transfer as ASCII text, FTP `TYPE A`) and `bool Append` (`-a`/`--append`: an FTP or SFTP upload appends instead of overwriting), both `false` when not given, and `TransferContext` implements them as `init` properties defaulting to `false`.

## Context

- BL-632 parses the options into `CommandLineOptions.UseAscii` and `CommandLineOptions.Append` (`Curl.Cli.UnitLibrary`); BL-633 (FTP `TYPE A`, `APPE`, `--crlf` wiring in `Curl.Console/TransferContextFactory.cs`) and BL-571 (SFTP `-a`) consume them and cannot without these members. BL-633's Context says to file this contract change separately rather than widen it.
- Files: `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` (follow the `FtpCreateDirectories` / `ConvertLineEndings` doc comment style; remark which schemes read each) and `TransferContext.cs` (the only implementer). Wiring from `CommandLineOptions` in `Curl.Console` is BL-633's, not this task's.

## Acceptance criteria

- [ ] `ITransferContext.UseAscii` and `ITransferContext.Append` exist with XML doc comments; `TransferContext` implements both, defaulting to `false`.
- [ ] `Curl.Protocol.Abstractions.UnitTests/TransferContextTests.cs` covers the defaults and an `init` of each to `true`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
