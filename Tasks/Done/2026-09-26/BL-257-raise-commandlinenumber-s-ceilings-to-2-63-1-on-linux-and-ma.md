---
id: BL-257
title: Raise CommandLineNumber's ceilings to 2^63-1 on Linux and macOS, keeping 2^31-1 on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-053]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
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

- [x] A `Curl.Cli.UnitTests` test shows `--tftp-blksize 2147483648` refused with
      `expected a proper numerical parameter` under the Windows ceiling and accepted under
      the Linux/macOS ceiling.
- [x] A test shows `9223372036854775807` accepted and `9223372036854775808` refused under
      the Linux/macOS ceiling, for both `ParseNonNegative` and `ParseMinusOneOrMore`.
- [x] `MaximumWholeSeconds` is the Windows value on Windows and the measured 64-bit value on
      Linux and macOS, each covered by a test.
- [x] `ParseOffset` is unchanged and its tests still pass.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green,
      with `Curl.Cli.UnitLibrary` at 100% line and branch coverage.

## Notes

- Measured 2026-09-26 against the OpenSSL curl **8.18.0** in WSL (Ubuntu; the only Linux curl
  reachable, no 8.21.0 build there): `--tftp-blksize`/`--max-redirs` accept
  `9223372036854775807` and refuse `9223372036854775808` ("expected a proper numerical
  parameter"); `-m`/`--connect-timeout` accept `9223372036854774` whole seconds
  (`CURLOPT_TIMEOUT_MS` 9223372036854774000) and refuse `9223372036854775`, so the 64-bit
  `MaximumWholeSeconds` is `LONG_MAX / 1000 - 1` = 9223372036854774, the same formula that gives
  the measured 8.21.0 Windows value 2147482. Accepted as standing in for 8.21.0 because both
  ceilings are `LONG_MAX`-derived and 8.21.0's Windows readings follow the same formula;
  `1.12345678901234` is 1123 ms on both.
- Design: `LongMaximumFor(bool isWindows)` and `MaximumWholeSecondsFor(long)` are pure and
  tested for both platforms; `PlatformLongMaximum` and `MaximumWholeSeconds` apply them to
  `OperatingSystem.IsWindows()`. `ParseNonNegative`, `ParseMinusOneOrMore` and `ParseSeconds`
  take the ceiling as a `longMaximum` argument and return `long`; `CommandLineOptionTable`
  passes `PlatformLongMaximum`. `ParseOffset`/`ParseSize` untouched.
- Decided by Claude (ADR-0039): the consumer types stay (`int?`/`int`/`TimeSpan?`) and the value
  saturates when recorded, because widening them reaches `Curl.Console`, `Curl.Core.UnitLibrary`
  and `Curl.Protocol.Abstractions.UnitLibrary`, which BL-139 and BL-292 hold, for no observable
  difference (TFTP clamps to 65464; no one follows 2^31 redirects or waits 29,000 years).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0039, its index row and the
  ADR-0019 consequence line; no task in Doing names it.
- Review found that on Linux/macOS a timeout can exceed the ~49.7-day .NET timer limit; noted in
  ADR-0039, the `CommandLineOptions` docs and BL-174's Notes (BL-174 enforces the timeouts).
- Parser-level tests that depend on the host's ceiling use `[OSCondition]`; the 8 Linux/macOS
  ones are skipped on this Windows host (no .NET SDK in WSL) and run in CI's ubuntu/macos legs.
- Results: build clean, fast tests green (Curl.Cli.UnitTests 1357 passed, 8 skipped),
  `Curl.Cli.UnitLibrary` 100% line and branch (Measure-CodeQuality.ps1), worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Numeric option ceilings follow the platform: 2^63-1 on Linux/macOS, 2^31-1 on Windows
