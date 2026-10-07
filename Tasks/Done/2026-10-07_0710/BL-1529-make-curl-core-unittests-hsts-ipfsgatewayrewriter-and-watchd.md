---
id: BL-1529
title: Make Curl.Core.UnitTests' Hsts, IpfsGatewayRewriter and watchdog tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1529 — Make Curl.Core.UnitTests' Hsts, IpfsGatewayRewriter and watchdog tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: every file in `Hsts/` (57 tests), `IpfsGatewayRewriterTests.cs`, `LowSpeedWatchdogTests.cs`, `LowSpeedWatchdogDiagnosticLogTests.cs`, `MaxTimeWatchdogTests.cs`, `MaxTimeWatchdogDiagnosticLogTests.cs` (115 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; `FakeTimeProvider` or `HandFiredTimeProvider` advances are `ARRANGE` lines; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~Curl.Core.Hsts.|FullyQualifiedName~IpfsGatewayRewriterTests|FullyQualifiedName~LowSpeedWatchdog|FullyQualifiedName~MaxTimeWatchdog" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the twelve files, before -> after: `Assert.` 199 -> 204, `[TestMethod` 115 -> 115, `[DataRow(` 191 -> 191. The extra asserts check the exception type the `ThrowsExactly` tests now capture; where an act result was asserted inline it is now captured in a local first, with the same expected values.
- The filtered detailed run printed 276 `END` lines (276 passed, 1 skipped off-platform test with no END line), none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line, so no follow-up task.
- Printed values are fixed test inputs (URLs, `HOME=/home/u`, fake-time values), nothing from the machine or operating system.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 115 Hsts, IpfsGatewayRewriter and watchdog tests write ARRANGE, ACT and ASSERT diagnostics
