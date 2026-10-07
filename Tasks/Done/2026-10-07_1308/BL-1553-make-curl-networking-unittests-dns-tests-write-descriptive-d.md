---
id: BL-1553
title: Make Curl.Networking.UnitTests' Dns tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1553 — Make Curl.Networking.UnitTests' Dns tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `Dns*Tests.cs` files (`DnsAnswerDecoderTests` through `DnsSourceBindingTests`: 12 files, 134 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs (DNS queries and replies through `BYTES`), Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.Dns" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- 2026-10-07 (lane 1): all 12 `Dns*Tests.cs` files now write ARRANGE, ACT, ASSERT/DIFF and BYTES lines through `TestDiagnostics`, in the pattern BL-1552 used (a `TestContext` property and a `Diagnostics` accessor per class). Repeated output is in small private writers: `DnsAnswerDecoderTests.Decode` (writes the message as BYTES, the asked type, and every field of the answer), `DnsServerResolverTests.ArrangeLookup`/`WriteResolution`/`WriteServiceLookup`/`WriteSent` (every sent query as BYTES with transport, server, local address and fake-clock time), `DnsFilterTraceEventsTests.AssertLines`, `DnsQueryEncoderTests.WriteEncoding`, `DnsServerQueryTests.WriteOutcome`. Every original assertion is kept as it was.
- Counts in the 12 files, before -> after: `Assert.` 219 -> 221, `[TestMethod` 133 -> 133, `[DataRow(` 115 -> 115. The two extra are `Assert.IsNotNull(servers)` in `DnsServerListTests`: once the `TryParse` result moved into a local, the compiler's nullable flow no longer knew `servers` was set, and the assertion says so where `!` would only have silenced it. (The Goal's "134 test methods" was a miscount; there are 133 `[TestMethod]`s, 222 test cases with data rows.)
- `dotnet test ... --filter "FullyQualifiedName~Curl.Networking.Dns" --logger "console;verbosity=detailed"`: 222 passed, 222 `END` lines, none with arrange, act or assert 0.
- `SLOW:` lines: none. No test here has phases, so none writes `PHASE`.
- Output is platform-neutral: only IP addresses, fixed end points, enum names and bytes are printed; the one real socket test (`DnsSocketOpenerTests`) prints the bound address, not its OS-chosen port.
- `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"`: 3034 passed, 29 skipped (platform-conditioned), 0 failed. `dotnet format --verify-no-changes` clean on the 12 files.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Dns*Tests.cs test in Curl.Networking.UnitTests writes ARRANGE, ACT and ASSERT diagnostics; 222 END lines, none empty
