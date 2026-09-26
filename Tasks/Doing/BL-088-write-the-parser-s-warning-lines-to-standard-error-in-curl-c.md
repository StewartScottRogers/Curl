---
id: BL-088
title: Write the parser's warning lines to standard error in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-051, BL-068]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-088 — Write the parser's warning lines to standard error in Curl.Console

## Goal

`Curl.Console` writes every line of `CommandLineParseResult.WarningLines` to standard
error, each ending in `Environment.NewLine`, before anything else it writes, on both
accepted and refused command lines, matching curl 8.21.0.

## Context

- BL-051 added `CommandLineParseResult.WarningLines` (`IReadOnlyList<string>`, ordered,
  no line terminators, carried on accepted and refused results) and the first warning,
  `CommandLineWarning.FileNameLooksLikeFlag(string)`:
  `Warning: The filename argument '<value>' looks like a flag.`, raised by `-o`/`--output`
  when the value starts with `-` and is longer than one character.
- BL-068 builds the runner in `Curl.Console` that parses the command line, prints a
  refusal's `StandardErrorLines` and returns its exit code, and otherwise runs the
  transfers; its standard output and standard error streams are injected so
  `Curl.Console.UnitTests` uses `MemoryStream`s. This task extends that runner; the
  line terminator is the console's choice (`Environment.NewLine`), not the parser's.
- Measured with the local curl 8.21.0 on Windows, 2026-09-26:
  - The warning lines come on stderr before anything else.
  - They are still printed when the command line is then refused:
    `curl -o -s --bogus` prints the warning for `-s`, then
    `curl: option --bogus: is unknown` and the try-help line, and exits 2.
  - Whether `-s` suppresses the warning is not settled: `curl -s -o -x file:///x` was
    seen to print it, but another run printed nothing extra. Before coding, measure with
    local curl 8.21.0 (`curl --version` to confirm) `curl -s -o -x file:///C:/Windows/win.ini`,
    `curl -sS -o -x file:///C:/Windows/win.ini` and `curl -o -x file:///C:/Windows/win.ini`
    (in a scratch directory, since `-x` is written as a file), capturing stderr
    separately, and record in this task's `Notes` whether each prints the warning.
    Implement what was measured. If `-s` suppresses it on an accepted command line,
    also measure `curl -s -o -x --bogus` and record whether a refused command line
    still prints the warning.
- Keep `Curl.Console` at 100% line and branch coverage and every method at cyclomatic
  complexity 10 or less.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test runs `-o -s --bogus`: stderr is exactly
      `Warning: The filename argument '-s' looks like a flag.`, then
      `curl: option --bogus: is unknown`, then the try-help line, each followed by
      `Environment.NewLine`; stdout is empty; the exit code is 2.
- [ ] A test runs an accepted command line with a flag-like `-o` value against a
      `file://` URL and asserts the warning line is the first thing on stderr and is
      written before any transfer output.
- [ ] The `-s` / `-sS` / neither measurements are recorded in `Notes` with the curl
      version, and a test asserts the measured behaviour for `-s` and for `-sS`.
- [ ] `dotnet build Curl.Console -warnaserror` and `dotnet build Curl.Console.UnitTests -warnaserror` are clean.
- [ ] `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
