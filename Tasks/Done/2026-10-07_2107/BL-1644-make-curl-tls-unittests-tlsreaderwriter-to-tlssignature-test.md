---
id: BL-1644
title: Make Curl.Tls.UnitTests' TlsReaderWriter to TlsSignature tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1644 — Make Curl.Tls.UnitTests' TlsReaderWriter to TlsSignature tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `TlsReaderWriterTests`, `TlsSessionCodecTests`, `TlsSessionRecordTests` and `TlsSignatureTests` (4 files, 47 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.TlsReaderWriterTests.|FullyQualifiedName~Curl.Tls.TlsSessionCodecTests.|FullyQualifiedName~Curl.Tls.TlsSessionRecordTests.|FullyQualifiedName~Curl.Tls.TlsSignatureTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts (`Assert.`, `[TestMethod`, `[DataRow(`) before -> after, unchanged in every file: TlsReaderWriterTests 24/7/4 -> 24/7/4; TlsSessionCodecTests 32/14/5 -> 32/14/5; TlsSessionRecordTests 5/4/4 -> 5/4/4; TlsSignatureTests 52/22/20 -> 52/22/20.
- The detailed run printed 70 `END` lines (47 methods, data rows expanded), none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line, so no follow-up task.
- Choices: where a `ThrowsExactly` result was discarded, the test now keeps the exception and writes its type name (assertion unchanged); `TlsSessionCodecTests` decodes through a private `Decode` that writes the DER bytes and the ACT line, and `WriteRefused` writes the refused ASSERT line; expression-bodied tests became block bodies with the same assertion on a local; the ML-DSA key generation sits in a `PHASE`.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the four files writes ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green
