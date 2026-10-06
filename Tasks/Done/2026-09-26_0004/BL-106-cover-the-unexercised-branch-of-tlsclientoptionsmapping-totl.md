---
id: BL-106
title: Cover the unexercised branch of TlsClientOptionsMapping.ToTlsMinimumVersion
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-106 — Cover the unexercised branch of TlsClientOptionsMapping.ToTlsMinimumVersion

## Goal

Every branch of `TlsClientOptionsMapping.ToTlsMinimumVersion` in
`Curl.Console/TlsClientOptionsMapping.cs` is exercised by a test, so `Curl.Console` is back
at 100% branch coverage.

## Context

- `ToTlsMinimumVersion(SslProtocols? minimumTlsVersion)` (line 31) is a private switch
  expression over a nullable `SslProtocols`: `Tls12` maps to `TlsMinimumVersion.Tls12`,
  `Tls13` to `TlsMinimumVersion.Tls13`, and `_` to `TlsMinimumVersion.SystemDefault`.
- `Measure-CodeQuality.ps1`, run 2026-09-26 on `factory/lane-2`, reports line 31 at
  83.33% condition coverage (5 of 6), leaving `Curl.Console` at 99.04% branch coverage
  against the 100% gate. The gap has been noted since BL-071 (see BL-070 and BL-095 in
  `Tasks/Done`); found again while working BL-098, not caused by it.
- The missing outcome is the `_` arm taken by a **non-null** value that is neither `Tls12`
  nor `Tls13` (for example `SslProtocols.Tls11` or `SslProtocols.None`). The existing
  tests in `Curl.Console.UnitTests/TlsClientOptionsMappingTests.cs` cover null, `Tls12`
  and `Tls13` only.
- **A command-line test cannot reach it.** `CommandLineOptions.MinimumTlsVersion` has an
  `internal` setter in `Curl.Cli.UnitLibrary`, which grants no `InternalsVisibleTo` to
  `Curl.Console.UnitTests`, and the parser sets it only in
  `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` lines 61-62, to `Tls12` or `Tls13`.
  So `Curl.Console` must change too. `Curl.Console.csproj` already declares
  `InternalsVisibleTo Curl.Console.UnitTests`, so the direct route is to make
  `ToTlsMinimumVersion` `internal` (keep its XML doc accurate) and test it with a
  non-null, non-Tls12/13 value. Do not touch `Curl.Cli.UnitLibrary`, and do not add
  `--tlsv1.0`/`--tlsv1.1` parsing here; that would be a behaviour change and a separate task.
- The mapping's behaviour must not change: every existing test in
  `TlsClientOptionsMappingTests` still passes unmodified.

## Acceptance criteria

- [x] `Curl.Console.UnitTests/TlsClientOptionsMappingTests.cs` contains a test named
  `ToTlsMinimumVersion_OtherNonNullVersion_MapsToSystemDefault` that passes a non-null
  `SslProtocols` value other than `Tls12` and `Tls13` (e.g. `SslProtocols.Tls11`) and
  asserts `TlsMinimumVersion.SystemDefault`.
- [x] The six existing tests in `TlsClientOptionsMappingTests` pass unchanged.
- [x] `dotnet build Curl.Console -warnaserror` and
  `dotnet build Curl.Console.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports
  `TlsClientOptionsMapping` at 100% branch coverage (line 31 at 6 of 6 conditions), and
  `Curl.Console` introduces no new line or branch gap.

## Notes

- Made `ToTlsMinimumVersion` `internal` with an XML doc comment, and tested it directly;
  `FromCommandLine` and the mapping's behaviour are unchanged.
- Test value is `SslProtocols.None`, not the suggested `Tls11`: `SslProtocols.Tls11` carries
  `[Obsolete]` (SYSLIB0039), which warnings-as-errors turns into a build break. `None` is
  non-null and neither Tls12 nor Tls13, so it takes the same `_` arm.
- Coverage: the Cobertura report shows the switch (now line 43, after the doc comment) at
  6/6 conditions and the `curl` package at line-rate 1, branch-rate 1.
- `Measure-CodeQuality.ps1` does not print a Curl.Console row at all: the assembly is
  `curl.dll`, and the script matches only the name `Curl.Console`. Verified from the raw
  Cobertura file instead; filed as BL-109.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ToTlsMinimumVersion fully branch-covered; Curl.Console back at 100% branch coverage
