---
id: BL-110
title: Cover CurlTransports' record copy constructor and init setters in Curl.Console tests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-110 — Cover CurlTransports' record copy constructor and init setters in Curl.Console tests

## Goal

`Curl.Console` reaches 100% line coverage because tests in `Curl.Console.UnitTests` exercise
the compiler-generated copy constructor and `init` setters of the `CurlTransports` record.

## Context

Found while working BL-109 (which made `Measure-CodeQuality.ps1` report `Curl.Console`).
Running `powershell -NoProfile -File Measure-CodeQuality.ps1 -SkipTestRun -Library Curl.Console`
after a test run shows `Curl.Console` at 97.88% line, 100% branch, with 8 failing members, all
in `Curl.Console\CurlTransports.cs` lines 16-23 and all at 0% line coverage:

- the copy constructor `CurlTransports..ctor(CurlTransports)`
- the `init` setters `set_DnsResolver`, `set_TimeProvider`, `set_TcpDialer`,
  `set_TlsClientOptions`, `set_TlsProvider`, `set_TcpConnector`, `set_UdpDatagramConnector`

The root `CLAUDE.md` holds `Curl.Console` to 100% line coverage.

`CurlTransports` is an `internal sealed record` with positional parameters; `Curl.Console.csproj`
already declares `InternalsVisibleTo Include="Curl.Console.UnitTests"`. The existing
`CreateTransports_*` tests in `Curl.Console.UnitTests\CurlCompositionTests.cs` obtain an
instance through `CurlComposition.CreateTransports(NoOptions())`. A `with` expression runs the
copy constructor and then the `init` setter of each property it names, so one `with` per
property (or one `with` naming all seven, taking replacement values from a second
`CreateTransports` call) covers every failing member. Assert the observable result: the named
property holds the replacement value and the others are carried over from the original.

Prefer a test-only fix. Do not restructure `CurlTransports` and do not touch `Curl.Console`
production code; if that proves impossible, stop and move the task to `Blocked` saying why, so
`touches` can be widened.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` (for example
  `CurlTransports_WithExpression_ReplacesNamedPropertyAndCopiesTheRest`) uses `with` on a
  `CurlTransports` from `CurlComposition.CreateTransports`, sets each of the seven properties,
  and passes.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean.
- [ ] `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` (after a test
  run, or with `-SkipTestRun` on fresh coverage) reports `Curl.Console` at 100% line and 100%
  branch with 0 failing members.
- [ ] No `[ExcludeFromCodeCoverage]` attribute is added anywhere, and no file outside
  `Curl.Console.UnitTests` changes.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
