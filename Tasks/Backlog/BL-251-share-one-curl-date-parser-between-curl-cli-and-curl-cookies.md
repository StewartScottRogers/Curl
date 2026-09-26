---
id: BL-251
title: Share one curl date parser between Curl.Cli and Curl.Cookies
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-251 — Share one curl date parser between Curl.Cli and Curl.Cookies

## Goal

One port of libcurl's `parsedate` serves both `-z` (`Curl.Cli`) and cookie `Expires` (`Curl.Cookies`), and `-z` refuses a year before 1583 as curl does.

## Context

- Filed by BL-219 (2026-09-26). `Curl.Cli.UnitLibrary/CurlDateParser.cs` returns a `DateTimeOffset`; BL-219 added `Curl.Cookies.UnitLibrary/CookieDateParser.cs`, the same port returning Unix seconds with curl's whole 64-bit range and its `yearnum < 1583` refusal. `Curl.Cookies` references only `Curl.Protocol.Abstractions.UnitLibrary`, so it could not reuse the `Curl.Cli` type.
- `CurlDateParser` has no 1583 floor, so `-z "1 Jan 1500"` is read where curl 8.21.0 treats the date as illegal (measured for cookies in BL-219: `Expires=Wed, 09 Jun 1582 10:18:14 GMT` is refused, 1583 is read).
- The shared home must be visible to both libraries: `Curl.Protocol.Abstractions.UnitLibrary` is the only project both already reference. Decide the home in an ADR marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [ ] An ADR under `Documentation/Planning/Decisions/` records where the shared parser lives and why.
- [ ] Only one `parsedate` port remains in the solution; `Curl.Cli` and `Curl.Cookies` both call it, and `CurlDateParserTests` and `CookieDateParserTests` pass against it (moved with it where they belong).
- [ ] `-z "1 Jan 1500"` is refused as an illegal date, as curl 8.21.0 refuses it (measured, the command and output recorded in `Notes`).
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-26: Created.
