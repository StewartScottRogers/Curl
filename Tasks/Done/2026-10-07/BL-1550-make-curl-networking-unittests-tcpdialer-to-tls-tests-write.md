---
id: BL-1550
title: Make Curl.Networking.UnitTests' TcpDialer to Tls tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1550 — Make Curl.Networking.UnitTests' TcpDialer to Tls tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' test files `TcpDialerTests.cs` through `TlsVersionRangeTests.cs` in name order (`TcpDialer*`, `TcpIo*`, `TcpPending*`, `TcpSocket*` and every `Tls*` test file: 13 files, 131 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.TcpDialer|FullyQualifiedName~Curl.Networking.TcpIo|FullyQualifiedName~Curl.Networking.TcpPending|FullyQualifiedName~Curl.Networking.TcpSocket|FullyQualifiedName~Curl.Networking.Tls" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 13 files, before -> after: `Assert.` 232 -> 233, `[TestMethod` 127 -> 127, `[DataRow(` 99 -> 99.
- The filtered detailed run printed 236 `END` lines (240 tests, 4 skipped by `OSCondition` on Windows), none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; the whole filtered run took about 2 s.
- Choices: where a test asserted on a call inline, the value is captured into a local for the diagnostics; `TcpDialerTests`' three `TryBindToDevice` tests now assert on that local rather than binding the socket twice. Helpers `LastLineOfABusyPortBind`, `AssertEndsQuietlyAsync` and `AssertMissingCloseNotifyAsync` became instance methods so they can write diagnostics. `TlsSessionCache.Take` consumes a session, so where an assertion itself calls `Take` the ASSERT line does not call it again. Tests comparing against OS exception text log only whether the message has the expected prefix, so nothing printed depends on the OS.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every TcpDialer to TlsVersionRange test in Curl.Networking.UnitTests writes ARRANGE, ACT and ASSERT diagnostics
