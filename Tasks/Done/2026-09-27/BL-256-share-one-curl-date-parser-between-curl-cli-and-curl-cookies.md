---
id: BL-256
title: Share one curl date parser between Curl.Cli and Curl.Cookies
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-256 — Share one curl date parser between Curl.Cli and Curl.Cookies

## Goal

One port of libcurl's `parsedate` serves both `-z` (`Curl.Cli`) and cookie `Expires` (`Curl.Cookies`), and `-z` refuses a year before 1583 as curl does.

## Context

- Filed by BL-219 (2026-09-26). `Curl.Cli.UnitLibrary/CurlDateParser.cs` returns a `DateTimeOffset`; BL-219 added `Curl.Cookies.UnitLibrary/CookieDateParser.cs`, the same port returning Unix seconds with curl's whole 64-bit range and its `yearnum < 1583` refusal. `Curl.Cookies` references only `Curl.Protocol.Abstractions.UnitLibrary`, so it could not reuse the `Curl.Cli` type.
- `CurlDateParser` has no 1583 floor, so `-z "1 Jan 1500"` is read where curl 8.21.0 treats the date as illegal (measured for cookies in BL-219: `Expires=Wed, 09 Jun 1582 10:18:14 GMT` is refused, 1583 is read).
- The shared home must be visible to both libraries: `Curl.Protocol.Abstractions.UnitLibrary` is the only project both already reference. Decide the home in an ADR marked "Decided by Claude under Stewart's delegation".

## Acceptance criteria

- [x] An ADR under `Documentation/Planning/Decisions/` records where the shared parser lives and why.
- [x] Only one `parsedate` port remains in the solution; `Curl.Cli` and `Curl.Cookies` both call it, and `CurlDateParserTests` and `CookieDateParserTests` pass against it (moved with it where they belong).
- [x] `-z "1 Jan 1500"` is refused as an illegal date, as curl 8.21.0 refuses it (measured, the command and output recorded in `Notes`).
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Decision (ADR-0074): the shared port is `Curl.Protocol.Abstractions.CurlDateParser`, the former
  `CookieDateParser` moved there (`TryParse(string, out long unixSeconds)`, 1583 floor, full 64-bit
  range). `Curl.Cli`'s copy is deleted; `CommandLineOptionTable.TryReadTimeConditionDate` calls the
  shared parser and caps the seconds into a `DateTimeOffset` (ADR-0073), in `TimeConditionInstant`.
- Tests: `CookieDateParserTests` moved to `Curl.Protocol.Abstractions.UnitTests/CurlDateParserTests`
  (plus `1 Jan 1500`, `31 Dec 1582 23:59:59 GMT`, `00000101` refused); `Curl.Cli`'s
  `CurlDateParserTests` duplicated those cases and was removed, its `DateTimeOffset`-specific cases
  (after-9999 cap, year 0, 1500, 1583) now in `CommandLineTimeConditionOptionTests`.
- Measured, curl 8.21.0 (Windows, Schannel), 2026-09-27:
  `curl -z "1 Jan 1500" file:///Z:/repos/Curl.lanes/lane-3/global.json -o NUL` prints
  `Warning: Illegal date format for -z, --time-cond (and not a filename). ` and
  `Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.`, then transfers,
  exit 0. `-z "1 Jan 1583"` prints no warning.
- Pipeline: a move of an existing, already-measured port with no new behaviour beyond the floor, so
  the plan/implement stages were done in-session rather than through separate agents.
- Gates: `dotnet build -warnaserror` clean; fast tests all green; `Measure-CodeQuality.ps1 -Library`
  100% line and branch, 0 failing members for Curl.Protocol.Abstractions, Curl.Cookies and Curl.Cli.
  `dotnet format --verify-no-changes` reports only ENDOFLINE on every CRLF working-copy file,
  untouched ones included (e.g. `Curl.Output.UnitLibrary/X509CertificateFields.cs`): pre-existing,
  not from this task.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. One parsedate port (Curl.Protocol.Abstractions.CurlDateParser) serves -z and cookie Expires; -z refuses a year before 1583 as curl does
