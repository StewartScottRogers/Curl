---
id: BL-257
title: Raise CommandLineNumber's ceilings to 2^63-1 on Linux and macOS, keeping 2^31-1 on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-053]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-257 — Raise CommandLineNumber's ceilings to 2^63-1 on Linux and macOS, keeping 2^31-1 on Windows

## Goal

On Linux and macOS, Curl accepts the numeric options curl reads into a C `long` up to 2^63-1, as the OpenSSL curl 8.21.0 there does; on Windows it still refuses anything above 2^31-1.

## Context

[ADR-0019](../../Documentation/Planning/Decisions/ADR-0019-numeric-option-ceiling-matches-the-platform-curl.md)
decides that the ceiling of a `long`-valued numeric option matches the platform curl:
2^31-1 on Windows (32-bit C `long`), 2^63-1 on Linux and macOS (64-bit C `long`).

Today `Curl.Cli.UnitLibrary/CommandLineNumber.cs` caps everywhere at `int.MaxValue`:
`ParseNonNegative` and `ParseMinusOneOrMore` read into an `int` through `TryReadDigits`,
and `MaximumWholeSeconds` (for `--connect-timeout` and `-m`) is `int.MaxValue / 1000 - 1`.
Make the ceiling a platform choice (`OperatingSystem.IsWindows()` or an injected value, so
both branches are unit-testable on one host), widen the outputs to `long` where the value
can exceed `int`, and carry the wider type through `CommandLineOptionTable` and
`CommandLineOptions` consumers (`--tftp-blksize`, `--max-redirs`, the timeouts).

`ParseOffset` (`-C`/`--continue-at`) and `ParseSize` already read a 64-bit value on every
platform and are out of scope.

Measure before pinning: the Linux ceiling and the `--connect-timeout`/`-m` maximum whole
seconds on 64-bit must come from the OpenSSL curl 8.21.0 on Linux (WSL or a CI runner), or
from curl 8.21.0's `src/tool_paramhlp.c` if no Linux curl is reachable; record which in Notes.

## Acceptance criteria

- [ ] A `Curl.Cli.UnitTests` test shows `--tftp-blksize 2147483648` refused with
      `expected a proper numerical parameter` under the Windows ceiling and accepted under
      the Linux/macOS ceiling.
- [ ] A test shows `9223372036854775807` accepted and `9223372036854775808` refused under
      the Linux/macOS ceiling, for both `ParseNonNegative` and `ParseMinusOneOrMore`.
- [ ] `MaximumWholeSeconds` is the Windows value on Windows and the measured 64-bit value on
      Linux and macOS, each covered by a test.
- [ ] `ParseOffset` is unchanged and its tests still pass.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green,
      with `Curl.Cli.UnitLibrary` at 100% line and branch coverage.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
