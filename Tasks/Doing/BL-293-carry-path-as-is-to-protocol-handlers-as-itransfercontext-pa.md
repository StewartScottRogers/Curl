---
id: BL-293
title: Carry --path-as-is to protocol handlers as ITransferContext.PathAsIs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-293 — Carry --path-as-is to protocol handlers as ITransferContext.PathAsIs

## Goal

`--path-as-is` reaches protocol handlers as `ITransferContext.PathAsIs`, and the `file`
handler honours it.

## Context

- `CommandLineOptions.PathAsIs` (`Curl.Cli.UnitLibrary`) already parses the option, but
  nothing carries it on. `FileProtocolHandler.ExecuteAsync` calls the
  `FileUrlPath.TryParse` overload that always removes dot segments.
- ADR-0010 (Accepted) names this as a step every option needed. It does not depend on
  `CurlUrl`; BL-294 later passes the same flag to `CurlUrl.TryParse`.
- ADR-0003 records curl 8.21.0 quoting `file:///C:/dir\..\x` under `--path-as-is` as
  `C:/dir/../x`. Measure again before pinning.

## Acceptance criteria

- [ ] `ITransferContext.PathAsIs` exists, `TransferContext` implements it, and `TransferContextFactory` sets it from `CommandLineOptions.PathAsIs` (tested in `Curl.Console.UnitTests`).
- [ ] `FileProtocolHandler` passes it to `FileUrlPath.TryParse`; a test shows dot segments kept with it and removed without it, matching curl 8.21.0 (measured, recorded under Notes).
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for the touched libraries.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
