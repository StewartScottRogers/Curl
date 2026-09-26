---
id: BL-089
title: Warn when a --cert, --key or --capath value looks like a flag
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-051]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-089 — Warn when a --cert, --key or --capath value looks like a flag

## Goal

`CommandLineParser` adds curl 8.21.0's `Warning: The filename argument '<value>' looks
like a flag.` line to `CommandLineParseResult.WarningLines` when the value of `--cert`
(`-E`), `--key` or `--capath` starts with `-` and is longer than one character, as it
already does for `-o`/`--output`, and `--cacert` does whatever curl 8.21.0 is measured
to do.

## Context

- BL-051 added `CommandLineParseResult.WarningLines` (ordered, no line terminators,
  carried on accepted and refused results), `CommandLineWarning.FileNameLooksLikeFlag(string)`
  and the row factory `CommandLineOption.FileName(longName, shortName, set)` in
  `Curl.Cli.UnitLibrary/CommandLineOption.cs` (Text plus the warning when the value
  starts with `-` and is longer than one character). Only the `output` row in
  `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` uses it so far.
- Measured with the local curl 8.21.0 on Windows, 2026-09-26: `--cert -x`, `--key -x`
  and `--capath -x` each print `Warning: The filename argument '-x' looks like a flag.`;
  `--ciphers -x`, `--url -x` and `-d -x` print no warning. `-E` is `--cert`'s short name.
- The change for the three measured options: in `CommandLineOptionTable`, turn the
  `capath`, `cert` (`'E'`) and `key` rows from `CommandLineOption.Text` into
  `CommandLineOption.FileName`, keeping their setters.
- `--cacert` was not measured for the warning. Its row is `CommandLineOption.Value("cacert",
  null, SetCaCertificateFile)`, which refuses a path that does not exist with
  `CommandLineRefusal.FileDoesNotExist`, so `FileName` does not fit it as is. Before
  coding, measure with local curl 8.21.0 (`curl --version` to confirm), in a scratch
  directory: `curl --cacert -x file:///C:/Windows/win.ini` with no file named `-x`
  present, and again after creating an empty file named `-x`. Record in this task's
  `Notes` whether the warning is printed in each case and, when the path is refused,
  whether the warning comes before or after the refusal lines. Implement exactly that
  (warning added to `WarningLines` on the accepted or refused result, or not at all);
  do not guess.
- Keep every method at cyclomatic complexity 10 or less (`CodeMetricsConfig.txt`) and
  `Curl.Cli.UnitLibrary` at 100% line and branch coverage.

## Acceptance criteria

- [ ] Tests in `Curl.Cli.UnitTests` parse `--cert -x URL`, `-E -x URL`, `--key -x URL`
      and `--capath -x URL`; each result is accepted and its `WarningLines` is exactly
      `["Warning: The filename argument '-x' looks like a flag."]`.
- [ ] A test parses `--ciphers -x URL`; the result is accepted and `WarningLines` is empty.
- [ ] The `--cacert -x` measurements (file absent, file present) are recorded in `Notes`
      with the curl version, and a test per case asserts the measured `WarningLines`
      and, when refused, the refusal lines and exit code 2.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean.
- [ ] `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
