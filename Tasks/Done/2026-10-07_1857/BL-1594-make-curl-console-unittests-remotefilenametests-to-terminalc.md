---
id: BL-1594
title: Make Curl.Console.UnitTests' RemoteFileNameTests to TerminalColumnsTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1594 — Make Curl.Console.UnitTests' RemoteFileNameTests to TerminalColumnsTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (13 files, 95 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `RemoteFileNameTests.cs`, `RemoteHeaderNameStreamTests.cs`, `RemoteTimeFailureWarningTests.cs`, `ResponseWaitTimerTraceEventsTests.cs`, `RetryPolicyMappingTests.cs`, `RoutingFtpProtocolHandlerTests.cs`, `RunDiagnosticLogTests.cs`, `Socks5AuthenticationMappingTests.cs`, `SshKnownHostsFileSearchTests.cs`, `StandardOutputFailureDeferringStreamTests.cs`, `StandardOutputOpenerTests.cs`, `StandardOutputVirtualTerminalTests.cs`, `TerminalColumnsTests.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 13 files, before -> after: `Assert.` 171 -> 171, `[TestMethod` 95 -> 95,
  `[DataRow(` 58 -> 58.
- The detailed run of these classes printed 136 `END` lines (95 methods, data rows counted
  once each) and none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; the 136 ran in about 3 seconds.
- Where a test reads this process's console or standard handles (`TerminalColumnsTests`'
  `ThisProcess` tests, `StandardOutputOpenerTests`), the diagnostics print only whether the
  result is acceptable, never the width or handle value, so nothing printed depends on the
  operating system or the console. `Socks5AuthenticationMappingTests`' `Map` still passes
  `OperatingSystem.IsWindows()`, but only the fields that do not depend on it are printed.
- Shared private helpers (`NameFrom`, `Resolve`, `ReadConsoleColumns`, `Parse`, `ContextWith`)
  now write the Arrange and Act lines, so their static modifiers became instance ones; no
  test's inputs, calls or assertions changed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 95 tests in the 13 files write arrange, act and assert diagnostics; build clean, fast tests green
