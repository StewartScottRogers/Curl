---
id: BL-1634
title: Make Curl.Tls.UnitTests' Tls12 Cbc to ClientConnection tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1634 — Make Curl.Tls.UnitTests' Tls12 Cbc to ClientConnection tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls12CbcRecordTests`, `Tls12CipherSuiteTests` and `Tls12ClientConnectionTests` (3 files, 36 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls12CbcRecordTests.|FullyQualifiedName~Curl.Tls.Tls12CipherSuiteTests.|FullyQualifiedName~Curl.Tls.Tls12ClientConnectionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts before -> after (Assert. / [TestMethod / [DataRow( matches): Tls12CbcRecordTests 27/15/13 -> 27/15/13; Tls12CipherSuiteTests 20/7/40 -> 20/7/40; Tls12ClientConnectionTests 42/14/9 -> 42/14/9. Some original assertions now assert on a local the test captured for its ACT line instead of the inline call; no assertion was removed or weakened.
- The filtered detailed run prints 85 END lines (all passed), none with a zero arrange, act or assert count. Fragments and single inputs short enough go on ARRANGE lines as hex, because BYTES lines do not count as ARRANGE.
- EveryPaddingLength... writes one summary (records opened, records rejected) after its 256-length loop under PHASE "open every padding length" (92-152 ms) rather than 768 per-iteration lines.
- No test printed a SLOW: line; the longest phase was SecondConnectionResumes...'s "first handshake" at about 433 ms, well under the 3000 ms budget, so no follow-up task.
- Only the three test files and this task file changed; no helper added (the shared Tls12PipeDriver writes nothing).

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 36 tests in the three Tls12 files write ARRANGE, ACT and ASSERT or DIFF lines (PHASE for handshakes); 85 runs, no zero counts, no SLOW
