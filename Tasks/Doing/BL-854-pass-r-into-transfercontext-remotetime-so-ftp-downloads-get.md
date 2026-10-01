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
completed:
---
# BL-854 — Pass -R into TransferContext.RemoteTime so FTP downloads get their MDTM time

## Goal

`curl -R -o out ftp://host/dir/f.txt` stamps `out` with the time `MDTM` named, as curl 8.21.0 does, because `Curl.Console` sets `TransferContext.RemoteTime` from `-R`.

## Context

- BL-637 added `ITransferContext.RemoteTime` and taught the FTP handler to send `MDTM` under `-R` and report its time on `TransferResult.SourceLastWriteTimeUtc` (ADR-0093's BL-637 addendum). `Curl.Console` was held by another lane, so the wiring was left here.
- `Curl.Console/TransferContextFactory.cs` builds the context (next to `TimeCondition = options.TimeCondition`); `CommandLineOptions.RemoteTime` already holds `-R`, and `CurlCommandRunner` already applies `SourceLastWriteTimeUtc` through `IFileTimeSetter`.
- Measured: with `MDTM` answered `213 20260927123456`, curl stamped `out` `2026-09-27T12:34:56Z`.

## Acceptance criteria

- [ ] `TransferContextFactory` sets `RemoteTime = options.RemoteTime`, and a `Curl.Console.UnitTests` test pins it for `-R` and its absence.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
