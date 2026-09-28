---
id: BL-499
title: Parse -4/--ipv4 and -6/--ipv6
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-499 — Parse -4/--ipv4 and -6/--ipv6

## Goal

`-4`, `--ipv4`, `-6` and `--ipv6` parse into one `CommandLineOptions` property naming the address family to use (either, IPv4 only, IPv6 only), the last one given winning, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 4 (Blocker).
- Alias-table rows `ipv4` (`4`) and `ipv6` (`6`), no `--no-` form (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`).
- Acting on it (resolving and dialling) is BL-500.

## Acceptance criteria

- [ ] `-4`, `--ipv4`, `-6`, `--ipv6`, the bundles `-s4` and `-6s`, and `-4 -6` (IPv6 wins) are covered by `Curl.Cli.UnitTests` data rows; `--no-ipv4` is refused as curl refuses it (the existing not-reversible rule).
- [ ] The property is an enum named for what it holds, documented with XML comments.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
