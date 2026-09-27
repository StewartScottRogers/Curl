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
completed:
---
# BL-282 — Parse -g/--globoff and take URLs unglobbed

## Goal

`-g`/`--globoff` (and `--no-globoff`) parse into the command line, so a caller can take each URL with `UrlGlob.Unglobbed` instead of `UrlGlob.TryParse`.

## Context

- Filed from BL-207, which built `Curl.Core.Globbing.UrlGlob` (expansion, `#N`, and `UrlGlob.Unglobbed` for `-g`). No option in `Curl.Cli.UnitLibrary` records `-g` yet.
- Upstream: https://curl.se/docs/manpage.html#-g, curl 8.21.0. `-g` is a boolean; the last of `-g`/`--no-globoff` wins, as for other boolean options.
- Wiring the parsed flag into `Curl.Console` belongs to BL-240.

## Acceptance criteria

- [ ] `-g`, `--globoff` and `--no-globoff` parse into a named command-line property, with tests in `Curl.Cli.UnitTests`.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
