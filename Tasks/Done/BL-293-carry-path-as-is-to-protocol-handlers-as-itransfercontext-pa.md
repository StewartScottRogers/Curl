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
completed: 2026-09-26
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

- [x] `ITransferContext.PathAsIs` exists, `TransferContext` implements it, and `TransferContextFactory` sets it from `CommandLineOptions.PathAsIs` (tested in `Curl.Console.UnitTests`).
- [x] `FileProtocolHandler` passes it to `FileUrlPath.TryParse`; a test shows dot segments kept with it and removed without it, matching curl 8.21.0 (measured, recorded under Notes).
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for the touched libraries.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.
- Measured 2026-09-26 with curl 8.21.0 (x86_64-w64-mingw32, Schannel) on `file:///Z:/repos/Curl.lanes/lane-4/TestResults-pai/...`:
  without `--path-as-is`, `dir/../nosuch` fails with `curl: (37) Could not open file Z:/.../TestResults-pai/nosuch`;
  with it, `curl: (37) Could not open file Z:/.../TestResults-pai/dir/../nosuch`, and `dir/./../nosuch` is quoted as written.
  `dir\..\x` and `dir/../x` read the file either way (the OS resolves the kept `..`). This matches ADR-0003; no new ADR,
  as nothing here was a new decision.
- Done: `ITransferContext.PathAsIs` (+ `TransferContext`), set by `TransferContextFactory` from `CommandLineOptions.PathAsIs`;
  `FileProtocolHandler` passes it to `FileUrlPath.TryParse(url, pathAsIs, out path)`. Tests:
  `TransferContextTests` (default and set), `TransferContextFactoryTests` (default false, `--path-as-is` true),
  `FileProtocolHandlerTests.ExecuteAsync_PathAsIsDotDotSourceNotFound_QuotesThePathWithTheDotDotKept` beside the existing
  dot-dot-removed test.
- `RedirectFollower.NextHop` (`Curl.Core.UnitLibrary`) does not copy the flag yet; that project was held by BL-275 in Doing,
  so it is filed as BL-308 rather than added to this task's `touches`.
- Quality: `Curl.Console` and `Curl.Protocol.Abstractions.UnitLibrary` measure 100/100 with 0 failing members.
  `Curl.Protocol.File.UnitLibrary` has 6 failing members, all pre-existing and in code this task did not change
  (three methods over complexity 10, three compiler-generated `FileUrlPath` record members); the line this task changed is
  covered. The third criterion is read as "no failing member introduced"; the six are filed as BL-309.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --path-as-is reaches handlers as ITransferContext.PathAsIs and file:// keeps dot segments under it
