---
id: BL-262
title: Parse --http0.9 and --no-http0.9 into CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-191]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-074
created: 2026-09-26
completed: 2026-09-26
---
# BL-262 — Parse --http0.9 and --no-http0.9 into CommandLineOptions

## Goal

`--http0.9` sets a `CommandLineOptions` member that allows an HTTP/0.9 reply, and `--no-http0.9` turns it off, as curl 8.21.0 does.

## Context

- Found while working BL-191 (2026-09-26): `curl --http0.9 http://127.0.0.1:1/` and `curl --no-http0.9 http://127.0.0.1:1/` both reach the connect (exit 7) on `/mingw64/bin/curl` 8.21.0, so the option is a negatable flag. Today both spellings are refused as unknown.
- FR-074 in `Documentation/Product/Requirements.md` is the refusal of an HTTP/0.9 reply without this option; wiring the member into the HTTP handler is separate work.
- Add it as a `CommandLineOption.NegatableFlag` row in `CommandLineOptionTable` (see `Curl.Cli.UnitLibrary/CLAUDE.md`).

## Acceptance criteria

- [x] `--http0.9` sets the new member and `--no-http0.9` after it clears it; a test per spelling in `Curl.Cli.UnitTests`, including `--no-http0.9=x`.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Pipeline `feature`, delivered in-session: the change is one `NegatableFlag` row and one property, so the architect/implementer stages were not spawned (sensible default, unattended run).
- Member named `AllowHttp09Reply` (says what it does: accept a reply with no status line). Wiring it into the HTTP handler (FR-074) stays separate work.
- Measured `/mingw64/bin/curl` 8.21.0 on 2026-09-26: `--http0.9=x` and `--no-http0.9=x` both reach the connect (exit 7), so the attached value is ignored, as the parser already does for every negatable flag.
- Tests in `CommandLineTransferEncodingOptionTests`: default off, `--http0.9`, `--no-http0.9` after it, `--no-http0.9=x` after it, `--http0.9` after `--no-http0.9`. Curl.Cli.UnitTests 1359 passed, 8 skipped.
- `Measure-CodeQuality.ps1`: Curl.Cli.UnitLibrary 100% line, 100% branch, 0 failing members. The full coverage run hit 2 load-sensitive failures in Curl.Networking.UnitTests (TLS, the area BL-254 owns); that project passes 357/357 run on its own, so coverage was read with `-SkipTestRun`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --http0.9 sets CommandLineOptions.AllowHttp09Reply and --no-http0.9 clears it
