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
completed:
---
# BL-1635 — Make Curl.Tls.UnitTests' Tls12ClientHandshakeFailureTests tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls12ClientHandshakeFailureTests` (1 file, 61 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project - here above all the alert sent or received with its code, and the flight the handshake failed in - and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- The class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper (`Tls12TestServer`, `Tls12HandshakeDriver`) may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [ ] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls12ClientHandshakeFailureTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In this file the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.
