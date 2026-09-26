---
id: BL-125
title: Parse -R/--remote-time and --no-remote-time into CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-011
created: 2026-09-26
completed:
---
# BL-125 — Parse -R/--remote-time and --no-remote-time into CommandLineOptions

## Goal

`CommandLineParser.Parse` accepts `-R`, `--remote-time` and `--no-remote-time`, and
`CommandLineOptions.RemoteTime` (`bool`, `false` when not given) says whether the last
spelling asked for the remote time.

## Context

- Prerequisite of BL-079, which applies the time to the `-o` file. Today
  `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` has no row for it and
  `Curl.Cli.UnitLibrary/CommandLineOptions.cs` no property, so `-R` is refused as an
  unknown option.
- Upstream (manpage checked 2026-09-26, documenting curl 8.23.0,
  <https://curl.se/docs/manpage.html#-R>): "Providing this option multiple times produces
  no additional effect. Users can disable it with the `--no-remote-time` negation form."
- Add `CommandLineOption.NegatableFlag("remote-time", 'R', (options, on) => options.RemoteTime = on)`,
  as `--silent` and `--insecure` are declared. The table's remarks record, measured on curl
  8.21.0, that the last spelling of a negatable flag wins and that a short letter is never
  negated; the same holds here.

## Acceptance criteria

- [ ] Tests in `Curl.Cli.UnitTests` assert `RemoteTime` is `true` for `-R` and for
      `--remote-time`, `false` when absent, `false` for `-R --no-remote-time`, and `true`
      for `--no-remote-time -R`.
- [ ] `CommandLineOptionTable.Rows` contains the `remote-time` row with short name `R`, a
      flag that takes no value; a data row in
      `Curl.Cli.UnitTests/CommandLineOptionTableTests.cs`
      (`Rows_FirstTableOption_HasItsShortNameAndArity`) asserts it.
- [ ] `Curl.Cli.UnitLibrary/README.md` lists the option.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean, `dotnet test --filter
      "TestCategory!=Integration"` is green, and `Curl.Cli.UnitLibrary` keeps 100% line and
      branch coverage.

## Notes

Filed 2026-09-26 while re-planning BL-079, which was blocked because `-R` is not parsed.

## Log

- 2026-09-26: Created.
