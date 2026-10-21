---
id: BL-1551
title: Make Curl.Networking.UnitTests' U tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1551 — Make Curl.Networking.UnitTests' U tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `U*` test files (`UdpDatagramChannelTests`, `UdpDatagramConnectorTests`, `UnixSocketAddressTests`: 3 files, 42 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs (datagrams through `BYTES`), Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.U" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts of `Assert.` / `[TestMethod` / `[DataRow(` lines, before -> after (unchanged; `Diagnostics.Assert(` does not match `Assert.`):
  `UdpDatagramChannelTests.cs` 20/10/0 -> 20/10/0, `UdpDatagramConnectorTests.cs` 65/26/0 -> 65/26/0,
  `UnixSocketAddressTests.cs` 5/5/10 -> 5/5/10. The task's "42 test methods" was a miscount: there are 41.
- The filtered detailed run printed 49 `END` lines (41 methods, the two data-driven ones expanding to 10 rows),
  none with a zero count; the project's fast run passed 3034, skipped 29.
- No test printed a `SLOW:` line; none has a phase worth timing (each is one fake-driven call or one socket
  open), so no `PHASE` lines were added.
- Choices: ephemeral local ports are printed as "is ephemeral: True" rather than the port, so output does not
  change run to run; `UdpDatagramConnectorTests` writes each `DatagramOpenResult` through one private
  `ActResult` so all 26 tests describe results the same way. Assertions inside `Assert.Throws*` tests now keep
  the returned exception to print it; the assertion itself is unchanged.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every U* test in Curl.Networking.UnitTests writes ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green
