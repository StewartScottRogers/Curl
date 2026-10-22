---
id: BL-1635
title: Make Curl.Tls.UnitTests' Tls12ClientHandshakeFailureTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1635 — Make Curl.Tls.UnitTests' Tls12ClientHandshakeFailureTests tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls12ClientHandshakeFailureTests` (1 file, 61 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project - here above all the alert sent or received with its code, and the flight the handshake failed in - and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- The class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper (`Tls12TestServer`, `Tls12HandshakeDriver`) may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls12ClientHandshakeFailureTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In this file the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in `Tls12ClientHandshakeFailureTests.cs`, before -> after: `Assert.` 34 -> 34, `[TestMethod` 61 -> 61, `[DataRow(` 39 -> 39. The `Assert.ThrowsExactly` calls now keep the exception they return so its parameter name or message can be written; no assertion was removed or weakened.
- Choice: a private `AssertFails` in the class shadows `Tls12HandshakeDriver.AssertFails`. It writes the failure (alert name and code, origin) as ACT and the alert and completion as ASSERT, then calls the driver's `AssertFails` unchanged, so the call sites stay as they were. Each test's ARRANGE names the flight the server's message was tampered in (first or final) and what was tampered; tampered bytes go through `BYTES`. `Tls12HandshakeDriver` and `Tls12TestServer` were not changed.
- `PHASE` timings wrap the setup handshakes in the three tests that run one before the failing step (the two resumption tests and the message-after-completion test).
- Filtered run: 87 runs, 87 `END` lines, none matching `END .*(\(arrange 0,|, act 0,|, assert 0\))`. No test printed a `SLOW:` line, so no follow-up task.
- `dotnet build Curl.Tls.UnitTests -warnaserror` clean; `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"`: 1283 passed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Tls12ClientHandshakeFailureTests test writes ARRANGE, ACT and ASSERT lines; 87 runs with non-zero counts, none SLOW.
