---
id: BL-051
title: Warn when a filename argument looks like a flag
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-051 — Warn when a filename argument looks like a flag

## Goal

An accepted `CommandLineParseResult` carries the warning lines curl 8.21.0 prints while
reading the command line, in order, starting with
`Warning: The filename argument '<value>' looks like a flag.` for an `-o`/`--output`
value that starts with `-`.

## Context

Follow-up of BL-037 (the table-driven parser in `Curl.Cli.UnitLibrary`:
`CommandLineParser`, `CommandLineParseResult`, `CommandLineOptionTable.Rows`,
`CommandLineOption.Flag`/`Text`/`Value`). The parser has no way to report a non-fatal
message today: a result is either accepted with `Options` or refused with `Refusal`.

Measured with the local curl 8.21.0 on Windows (2026-09-26): `-o -s`,
`--output --output` and `-o --` all still parse - the value is taken as the file name -
but curl prints on stderr

```
Warning: The filename argument '<value>' looks like a flag.
```

with `<value>` the value exactly as given (`-s`, `--output`, `--`), and carries on.

Like refusal lines, warning lines are returned without line terminators; which newline
the console writes is the console layer's decision. Whether the warning is triggered by
a leading `-` alone, or by something narrower, must be measured with the local curl
(for example `-o -`, which curl treats as stdout, and `-o -x`) before the rule is coded;
record what was measured in the XML documentation of the code that decides it.

BL-078 (more output options than URLs) will append to the same warning list.

## Acceptance criteria

- [ ] `CommandLineParseResult` exposes the warning lines as an ordered
      `IReadOnlyList<string>`, empty when there are none, on accepted results.
- [ ] Tests in `Curl.Cli.UnitTests` assert that `-o -s URL`, `--output --output URL` and
      `-o -- URL` are each accepted, set `OutputFiles` to the flag-like value, and carry
      exactly one warning line, `Warning: The filename argument '-s' looks like a flag.`
      (and the `--output` and `--` equivalents).
- [ ] A test asserts that two flag-like file names (`-o -a -o -b URL`) give two warning
      lines in command-line order.
- [ ] A test asserts that `-o file URL` carries no warning.
- [ ] The measured behaviour for `-o -` is covered by a test that matches it.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
