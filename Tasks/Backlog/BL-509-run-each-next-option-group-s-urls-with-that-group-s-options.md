---
id: BL-509
title: Run each -:/--next option group's URLs with that group's options
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-508]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-509 — Run each -:/--next option group's URLs with that group's options

## Goal

`CurlCommandRunner` runs the groups BL-508 parses in order, each URL with its own group's options (method, headers, data, output, `-w`), shares the connection pool, cookie engine and transfer numbering across groups as curl 8.21.0 does, and exits with the code curl gives for a run in which some groups fail.

## Context

- Conformance audit 2026-09-28, row 8 (Blocker). Parsing is BL-508.
- Code: `Curl.Console/CurlCommandRunner.cs` (the per-URL loop), `TransferContextFactory.cs`, `UrlTransfer.cs`, `CurlComposition.cs`. Connection reuse across URLs is ADR-0050; `%{xfer_id}`/`%{conn_id}`/`%{urlnum}` numbering is ADR-0060.
- `--fail-early` stops at the first failure; without it curl continues and the final exit code is the last failure's (measure).
- Parallel transfers (`-Z`, BL-517 onwards) build on the group list this task introduces into the runner.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 3`: `curl -d a URL1 --next URL2` (POST then GET), a failing first group with and without `--fail-early`, `-w '%{urlnum} %{xfer_id} %{conn_id}\n'` in each group, and `-c jar` in one group; request bytes, stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Console.UnitTests` tests with a fake handler pin each measured case: request per group, output per group, numbering, exit code.
- [ ] A single-group command line behaves exactly as before (existing tests pass unchanged).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- From BL-508 (ADR-0125, BL-508 Notes): run `CommandLineParseResult.Groups` in order. The console
  already prints `RefusalAfterGroups` after the transfers. curl 8.21.0 runs no group after one
  that has an `-o` left over, and prints the "more output options than URLs" warning once for
  that group and once for each group after it (`WarningLinesAfterTransfers` already counts them).

## Log

- 2026-09-28: Created.
