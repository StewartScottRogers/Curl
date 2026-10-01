---
id: BL-854
title: Pass -R into TransferContext.RemoteTime so FTP downloads get their MDTM time
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-637]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-854 — Pass -R into TransferContext.RemoteTime so FTP downloads get their MDTM time

## Goal

`curl -R -o out ftp://host/dir/f.txt` stamps `out` with the time `MDTM` named, as curl 8.21.0 does, because `Curl.Console` sets `TransferContext.RemoteTime` from `-R`.

## Context

- BL-637 added `ITransferContext.RemoteTime` and taught the FTP handler to send `MDTM` under `-R` and report its time on `TransferResult.SourceLastWriteTimeUtc` (ADR-0093's BL-637 addendum). `Curl.Console` was held by another lane, so the wiring was left here.
- `Curl.Console/TransferContextFactory.cs` builds the context (next to `TimeCondition = options.TimeCondition`); `CommandLineOptions.RemoteTime` already holds `-R`, and `CurlCommandRunner` already applies `SourceLastWriteTimeUtc` through `IFileTimeSetter`.
- Measured: with `MDTM` answered `213 20260927123456`, curl stamped `out` `2026-09-27T12:34:56Z`.

## Acceptance criteria

- [x] `TransferContextFactory` sets `RemoteTime = options.RemoteTime`, and a `Curl.Console.UnitTests` test pins it for `-R` and its absence.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- `TransferContextFactory.Create` now copies `options.RemoteTime` beside `TimeCondition`. `Create_NoOptions_LeavesEveryOptionAtItsDefault` pins it false without `-R`; `Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine` passes `-R` and pins it true. Build clean with -warnaserror; fast tests green (Curl.Console.UnitTests 1950 passed).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. curl -R on an FTP download now sends MDTM and stamps the output file, because TransferContextFactory passes -R as TransferContext.RemoteTime
