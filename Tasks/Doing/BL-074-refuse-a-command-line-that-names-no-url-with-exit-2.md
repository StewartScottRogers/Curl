---
id: BL-074
title: Refuse a command line that names no URL with exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-074 — Refuse a command line that names no URL with exit 2

## Goal

`CommandLineParser.Parse` refuses a command line that parses cleanly but names no URL,
with `CurlExitCode.FailedInit` and curl 8.21.0's exact two stderr lines.

## Context

Follow-up of BL-037, which created the table-driven parser in `Curl.Cli.UnitLibrary`:
`CommandLineParser.Parse` returns a `CommandLineParseResult` (`IsAccepted`, `Options`,
`Refusal`); `CommandLineOptions.Urls` collects positional arguments and `--url` values.
Today a command line with options but no URL is accepted with an empty `Urls`.

Measured with the local curl 8.21.0 on Windows (2026-09-26): `curl -s`, `curl --` and
any other command line with arguments but no URL print

```
curl: (2) no URL specified
curl: try 'curl --help' or 'curl --manual' for more information
```

on stderr, nothing on stdout, exit 2 (`CURLE_FAILED_INIT`,
<https://curl.se/libcurl/c/libcurl-errors.html>).

The first line is not in the `curl: option <spelled>: <reason>` shape that every existing
`CommandLineRefusal` factory builds through its private constructor, so the refusal type
needs a second way to build its first line (for example a `NoUrlSpecified()` factory);
the second line is the existing `CommandLineRefusal.TryHelpLine`.

A refusal met while reading options (unknown option, requires parameter, blank argument,
numeric) still wins: the no-URL check runs only after every argument has been read.

`curl` with zero arguments was not measured for this task; do not assume it behaves the
same. Measure it with the local curl; if it matches, cover it here, and if it differs,
leave it unchanged and file a follow-up task with the measured output.

## Acceptance criteria

- [ ] `CommandLineRefusal` has a factory for the no-URL refusal whose
      `StandardErrorLines` are exactly `curl: (2) no URL specified` and
      `CommandLineRefusal.TryHelpLine`, and whose `ExitCode` is `CurlExitCode.FailedInit`.
- [ ] Tests in `Curl.Cli.UnitTests` assert that `Parse(["-s"])`, `Parse(["--"])` and
      `Parse(["-o", "file"])` are refused with exactly those two lines and exit 2.
- [ ] A test asserts that `Parse(["--bogus"])` still gives the `is unknown` refusal, not
      the no-URL refusal.
- [ ] A test asserts that `Parse(["-s", "http://example.com/"])` is still accepted.
- [ ] The behaviour of `curl` with zero arguments is recorded in this task's Notes with
      its measured stderr and exit code, and either covered by a test or filed as a
      follow-up task.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
