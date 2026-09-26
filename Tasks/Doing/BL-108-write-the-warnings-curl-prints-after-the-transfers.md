---
id: BL-108
title: Write the warnings curl prints after the transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-078]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-108 — Write the warnings curl prints after the transfers

## Goal

`Curl.Console` writes `CommandLineParseResult.WarningLinesAfterTransfers` to standard error
after the last transfer has ended, after any transfer error message, as curl 8.21.0 does.

## Context

Follow-up of BL-078, which added `CommandLineParseResult.WarningLinesAfterTransfers` in
`Curl.Cli.UnitLibrary` (today it holds `Warning: Got more output options than URLs`).
`Curl.Console/CurlCommandRunner.cs` already writes `parsed.WarningLines` before the
transfer; this second list must be written at the end instead.

Measured with the local curl 8.21.0 on Windows (2026-09-26): `curl -o f -o g file:///Z:/nx`
prints `curl: (37) Could not open file Z:/nx` and then
`Warning: Got more output options than URLs`; with a transfer that succeeds, the warning
follows the progress meter. `-s` anywhere on the command line drops it, which the parse
result already handles. The warning does not change the exit code (37 above).

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` asserts that standard error for
      `-o f -o g <file:// URL naming a missing file>` is the transfer's error line followed by
      `Warning: Got more output options than URLs`, and the exit code is 37.
- [ ] A test asserts the warning is written after a transfer that succeeds.
- [ ] A test asserts nothing extra is written for `-o f <URL>`.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
