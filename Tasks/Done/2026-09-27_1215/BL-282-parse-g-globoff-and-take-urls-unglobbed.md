---
id: BL-282
title: Parse -g/--globoff and take URLs unglobbed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-282 — Parse -g/--globoff and take URLs unglobbed

## Goal

`-g`/`--globoff` (and `--no-globoff`) parse into the command line, so a caller can take each URL with `UrlGlob.Unglobbed` instead of `UrlGlob.TryParse`.

## Context

- Filed from BL-207, which built `Curl.Core.Globbing.UrlGlob` (expansion, `#N`, and `UrlGlob.Unglobbed` for `-g`). No option in `Curl.Cli.UnitLibrary` records `-g` yet.
- Upstream: https://curl.se/docs/manpage.html#-g, curl 8.21.0. `-g` is a boolean; the last of `-g`/`--no-globoff` wins, as for other boolean options.
- Wiring the parsed flag into `Curl.Console` belongs to BL-240.

## Acceptance criteria

- [x] `-g`, `--globoff` and `--no-globoff` parse into a named command-line property, with tests in `Curl.Cli.UnitTests`.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Measured the local curl 8.21.0 (Schannel) on 2026-09-26: `curl -g --no-globoff "http://127.0.0.1:1/{a,b}"` makes two transfers, so `--no-globoff` is accepted and the last spelling wins; `--globoff=x` is accepted like every other boolean.
- Added `CommandLineOptions.GlobOff` and a `NegatableFlag("globoff", 'g')` row, the same shape as `-k`/`--insecure`; no ADR needed, since it follows the existing boolean-option rule.
- Delivered in the session rather than through the full `/feature` agent chain: the change is one table row and one property, and the plan is the existing negatable-flag pattern.
- Tests: `CommandLineGlobOffOptionTests` (default, `-g`, `--globoff`, both orders of negation, `-gs` bundling) and a `globoff` row in `CommandLineOptionTableTests`. Curl.Cli.UnitTests 1375 passed, 8 skipped (platform); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members.
- Wiring `GlobOff` into `Curl.Console` stays with BL-240.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -g/--globoff and --no-globoff parse into CommandLineOptions.GlobOff, last spelling wins
