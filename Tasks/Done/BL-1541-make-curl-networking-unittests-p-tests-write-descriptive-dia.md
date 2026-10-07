---
id: BL-1541
title: Make Curl.Networking.UnitTests' P tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1541 — Make Curl.Networking.UnitTests' P tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `P*` test files (`PeerVerificationTests` through `PreemptiveBasicProxyAuthenticatorTests`: 9 files, 120 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.P" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each class gained `TestContext` and a `Diagnostics => TestDiagnostics.For(TestContext)` property, the style of `TcpConnectorTests`; each test writes ARRANGE inputs, ACT results and ASSERT expected-against-actual lines, with `PHASE` scopes around the main call where one is natural.
- Counts in the 9 `P*` files, before -> after: `Assert.` 286 -> 286, `[TestMethod` 120 -> 120, `[DataRow(` 31 -> 31.
- In `PinnedPublicKeyTests`, `PoolingConnectorQuicSessionTests` and `PoolingConnectorSharedCacheTests`, a call made inside an assertion (`Assert.IsTrue(PinnedPublicKey.Matches(...))`) now stores its result in a local that the same assertion checks, so it can be printed; every assertion checks the same value as before.
- Nothing printed depends on the operating system: the generated certificate's hash and temporary paths are described, never printed.
- The detailed run printed 145 `END` lines (every test the filter runs), none with a zero count. No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in Curl.Networking.UnitTests' 9 P* files writes ARRANGE, ACT and ASSERT diagnostics
