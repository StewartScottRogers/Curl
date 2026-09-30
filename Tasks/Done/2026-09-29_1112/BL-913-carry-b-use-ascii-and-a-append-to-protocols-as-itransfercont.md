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
completed: 2026-09-29
---
# BL-913 — Carry -B/--use-ascii and -a/--append to protocols as ITransferContext.UseAscii and Append

## Goal

`ITransferContext` gains `bool UseAscii` (`-B`/`--use-ascii`: transfer as ASCII text, FTP `TYPE A`) and `bool Append` (`-a`/`--append`: an FTP or SFTP upload appends instead of overwriting), both `false` when not given, and `TransferContext` implements them as `init` properties defaulting to `false`.

## Context

- BL-632 parses the options into `CommandLineOptions.UseAscii` and `CommandLineOptions.Append` (`Curl.Cli.UnitLibrary`); BL-633 (FTP `TYPE A`, `APPE`, `--crlf` wiring in `Curl.Console/TransferContextFactory.cs`) and BL-571 (SFTP `-a`) consume them and cannot without these members. BL-633's Context says to file this contract change separately rather than widen it.
- Files: `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` (follow the `FtpCreateDirectories` / `ConvertLineEndings` doc comment style; remark which schemes read each) and `TransferContext.cs` (the only implementer). Wiring from `CommandLineOptions` in `Curl.Console` is BL-633's, not this task's.

## Acceptance criteria

- [x] `ITransferContext.UseAscii` and `ITransferContext.Append` exist with XML doc comments; `TransferContext` implements both, defaulting to `false`.
- [x] `Curl.Protocol.Abstractions.UnitTests/TransferContextTests.cs` covers the defaults and an `init` of each to `true`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly rather than through the full /feature agents: a two-member contract addition with no behaviour, so no plan or ADR was needed. `TransferContext` is the only implementer, so no fake elsewhere needed the members.
- Placed after `ListOnly` beside the other FTP members. Remarks name `ftp://` for `UseAscii` (and note `;type=A` asks for the same) and `ftp://` (`APPE`) plus `sftp://` for `Append`, the readers BL-633 and BL-571 will add.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Abstractions 608 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ITransferContext.UseAscii and Append carry -B and -a to protocols, default false
