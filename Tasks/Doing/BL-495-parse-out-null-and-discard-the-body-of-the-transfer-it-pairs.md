---
id: BL-495
title: Parse --out-null and discard the body of the transfer it pairs with
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-495 — Parse --out-null and discard the body of the transfer it pairs with

## Goal

`--out-null` takes an output slot like `-o` and discards that URL's body, so `curl --out-null URL -w '%{http_code}'` writes only the write-out, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `out-null` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- Output options pair with URLs as curl's tool pairs them (ADR-0029); `UrlOutput.cs` in Cli and `OutputFileTarget.cs` in Console are where a discarding target fits.
- `%{filename_effective}`, `-D`, `-i` and the progress meter behaviour with `--out-null` must be measured, not assumed.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--out-null` with one URL, with two URLs (one `--out-null`, one `-o`), with `-i`, and with `-w '%{filename_effective}|%{size_download}'`; stdout, stderr and exit code copied into Notes.
- [ ] `--out-null` parses and pairs with URLs in command-line order as ADR-0029 pairs `-o`; `Curl.Cli.UnitTests` covers the pairing.
- [ ] `Curl.Console.UnitTests` tests pin the measured stdout bytes and `-w` values; no file is created.
- [ ] New tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
