---
id: BL-1554
title: Make Curl.Networking.UnitTests' Doh, E and F tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1554 — Make Curl.Networking.UnitTests' Doh, E and F tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `Doh*`, `E*` and `F*` test files (`DohDnsResolverSubTransferTests` through `FlowScopedTransferEventsTests`: 11 files, 108 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.Do|FullyQualifiedName~Curl.Networking.E|FullyQualifiedName~Curl.Networking.F" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- 2026-10-07 (lane 1): all 11 files from `DohDnsResolverSubTransferTests` to `FlowScopedTransferEventsTests` now write ARRANGE, ACT, ASSERT/DIFF and BYTES lines through `TestDiagnostics`, in BL-1553's pattern (a `TestContext` property and a `Diagnostics` accessor per class). Shared writers: `DohDnsResolverTests.ArrangeResolve`/`WriteSent` (every POST as BYTES)/`WriteTargets`/`WriteAddresses`, `DohDnsResolverTraceTests.ArrangeAnswers` (every queued DoH answer as BYTES)/`WriteTrace` with a DIFF of the expected trace lines, `DohResponseReaderTests.ReadAsync` (response and body as BYTES), `DohDnsResolverSubTransferTests.WriteEventLinesAndLastLine`, `EarlyDataTlsConnectionTests.WriteState`, `EchResultTextTests.WriteResultText`, `EchRetryConfigsTextTests.WriteLines`. Every original assertion is kept; where a value is now read into a local first, the assertion checks the same value.
- Counts in the 11 files, before -> after: `Assert.` 160 -> 160, `[TestMethod` 107 -> 107, `[DataRow(` 78 -> 78. (The Goal's "108 test methods" was a miscount; there are 107 `[TestMethod]`s.)
- Targeted run (`--filter "FullyQualifiedName~Curl.Networking.Do|...E|...F"`, detailed logger): 168 tests, 167 passed, 1 skipped (`ApplySocketOptions_WithFastOpenOnLinux_SetsTcpFastOpenConnect`, Linux only); 167 `END` lines, none with arrange, act or assert 0.
- `SLOW:` lines: none. No test here has phases, so none writes `PHASE`.
- Output is platform-neutral: the one OS-dependent value, the length of the BCL's own PKCS#8 export in `Decrypt_OfAKeyTheBclEncrypted_GivesItsPrivateKeyInfo`, is not printed; the OS-conditioned FastOpen tests print only constants.
- `dotnet build Curl.Networking.UnitTests -warnaserror` clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"`: 3035 passed, 28 skipped (platform-conditioned), 0 failed. `dotnet format --verify-no-changes` clean on the 11 files.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Doh, E and F test in Curl.Networking.UnitTests writes ARRANGE, ACT and ASSERT diagnostics; 167 END lines, none empty
