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
completed: 2026-09-28
---
# BL-499 — Parse -4/--ipv4 and -6/--ipv6

## Goal

`-4`, `--ipv4`, `-6` and `--ipv6` parse into one `CommandLineOptions` property naming the address family to use (either, IPv4 only, IPv6 only), the last one given winning, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 4 (Blocker).
- Alias-table rows `ipv4` (`4`) and `ipv6` (`6`), no `--no-` form (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`).
- Acting on it (resolving and dialling) is BL-500.

## Acceptance criteria

- [x] `-4`, `--ipv4`, `-6`, `--ipv6`, the bundles `-s4` and `-6s`, and `-4 -6` (IPv6 wins) are covered by `Curl.Cli.UnitTests` data rows; `--no-ipv4` is refused as curl refuses it (the existing not-reversible rule).
- [x] The property is an enum named for what it holds, documented with XML comments.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- New enum `IpAddressFamilyChoice` (`Either`, `IPv4Only`, `IPv6Only`) in `Curl.Cli.UnitLibrary`; `CommandLineOptions.IpAddressFamily` holds it, set by `Flag` rows `ipv4`/`4` and `ipv6`/`6`. BL-500 can move or map it when it reaches the dialler.
- Measured curl 8.21.0 (Windows, 2026-09-28): `-4 -6` prints no warning (unlike `-0 --http1.1`), `-s4` and `-6s` bundle, `--no-ipv4` and `--no-ipv4=x` exit 2 as not reversible.
- Per-group option (curl keeps `ip_version` in each `OperationConfig`), so added to `PerGroupOptionLongNames`; `--next` resets it to `Either`.
- Tests: `CommandLineIpAddressFamilyOptionTests`. Coverage of Curl.Cli.UnitLibrary 100% line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -4/--ipv4 and -6/--ipv6 parse into CommandLineOptions.IpAddressFamily, last one winning
