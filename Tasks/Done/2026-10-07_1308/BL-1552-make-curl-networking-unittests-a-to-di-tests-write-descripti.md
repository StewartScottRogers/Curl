---
id: BL-1552
title: Make Curl.Networking.UnitTests' A to Di tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1552 — Make Curl.Networking.UnitTests' A to Di tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' files `AsyncResolveTeardownTraceEventsTests.cs` through `DigestStaleChallengeTests.cs` in name order (every `A*`, `C*`, `Da*`, `De*` and `Di*` test file: 21 files, 125 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Read BL-1468's Context: what matters in this project (endpoints, DNS replies, proxy and TLS settings, bytes through the fake transports, certificates by subject and thumbprint, `PHASE` lines for resolve, connect, proxy handshake and TLS handshake); line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every test class is in namespace `Curl.Networking`, so the filter below selects these files' classes by name prefix.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.A|FullyQualifiedName~Curl.Networking.C|FullyQualifiedName~Curl.Networking.Da|FullyQualifiedName~Curl.Networking.De|FullyQualifiedName~Curl.Networking.Di" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Before: 198 `Assert.`, 125 `[TestMethod`, 171 `[DataRow(` matches in the 21 files; after: 207, 125, 171. The extra `Assert.` lines capture exceptions that were only thrown before, so their type can be logged.
- The range filter ran 266 tests (1 skipped, the macOS-only `DarwinFastOpenConnectTests` test) and printed 266 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; no follow-up task.
- Nothing printed depends on the operating system: temp files are logged by file name and size, and `ConnectFailureReasonTests` logs only whether the reason equals the system message, not its text.
- `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"`: 3035 passed, 28 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every A to Di test in Curl.Networking.UnitTests writes ARRANGE, ACT and ASSERT diagnostics
