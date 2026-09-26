---
id: BL-082
title: Refuse an empty command line with curl's lone try-help line and exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-074]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-082 — Refuse an empty command line with curl's lone try-help line and exit 2

## Goal

`CommandLineParser.Parse([])` refuses an empty command line with `CurlExitCode.FailedInit`
and the single stderr line curl 8.21.0 prints for it.

## Context

Follow-up of BL-074, which made `CommandLineParser.Parse` refuse a command line that has
arguments but names no URL (`curl: (2) no URL specified` plus the try-help line) and left
an empty command line accepted, because curl answers it differently.

Measured with the local curl 8.21.0 (x86_64-w64-mingw32) on Windows, 2026-09-26: `curl`
with zero arguments prints nothing on stdout and exactly one line on stderr,

```
curl: try 'curl --help' or 'curl --manual' for more information
```

and exits 2 (`CURLE_FAILED_INIT`, <https://curl.se/libcurl/c/libcurl-errors.html>).
There is no `no URL specified` line.

`CommandLineRefusal.StandardErrorLines` today always holds two lines, the second being
`CommandLineRefusal.TryHelpLine`; this refusal needs a factory whose lines are only
`TryHelpLine`. The existing test `Parse_NoArguments_ReturnsDefaults` in
`Curl.Cli.UnitTests/CommandLineParserTests.cs` pins today's acceptance and must change.

## Acceptance criteria

- [ ] `CommandLineRefusal` has a factory for the empty-command-line refusal whose
      `StandardErrorLines` are exactly one line, `CommandLineRefusal.TryHelpLine`, and whose
      `ExitCode` is `CurlExitCode.FailedInit`.
- [ ] A test in `Curl.Cli.UnitTests` asserts that `Parse([])` is refused with exactly that
      one line and exit 2.
- [ ] The `CommandLineRefusal` and `CommandLineParser` XML docs and
      `Curl.Cli.UnitLibrary/README.md` no longer say an empty command line is accepted or
      that every refusal has two lines.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
