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
completed:
---
# BL-1552 — Make Curl.Networking.UnitTests' A to Di tests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' files `AsyncResolveTeardownTraceEventsTests.cs` through `DigestStaleChallengeTests.cs` in name order (every `A*`, `C*`, `Da*`, `De*` and `Di*` test file: 21 files, 125 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Read BL-1468's Context: what matters in this project (endpoints, DNS replies, proxy and TLS settings, bytes through the fake transports, certificates by subject and thumbprint, `PHASE` lines for resolve, connect, proxy handshake and TLS handshake); line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every test class is in namespace `Curl.Networking`, so the filter below selects these files' classes by name prefix.

## Acceptance criteria

- [ ] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.A|FullyQualifiedName~Curl.Networking.C|FullyQualifiedName~Curl.Networking.Da|FullyQualifiedName~Curl.Networking.De|FullyQualifiedName~Curl.Networking.Di" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
