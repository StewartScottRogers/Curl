---
id: BL-1542
title: Make Curl.Networking.UnitTests' Q to Sy tests outside SslStreamTlsProviderTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1542 — Make Curl.Networking.UnitTests' Q to Sy tests outside SslStreamTlsProviderTests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests`' `Q*`, `R*` and `S*` test files other than `SslStreamTlsProviderTests*.cs` (`QualityOfServiceSocketOptionsTests` through `SystemNetworkInterfaceLookupTests`: 16 files, 97 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.Q|FullyQualifiedName~Curl.Networking.R|FullyQualifiedName~Curl.Networking.Sc|FullyQualifiedName~Curl.Networking.Se|FullyQualifiedName~Curl.Networking.So|FullyQualifiedName~Curl.Networking.SslStreamConnection|FullyQualifiedName~Curl.Networking.Sspi|FullyQualifiedName~Curl.Networking.St|FullyQualifiedName~Curl.Networking.Sy" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Done in three parallel batches of files (by test-writer agents), following BL-1541's pattern: `using Curl.Testing;`, a `TestContext` property, `Diagnostics => TestDiagnostics.For(TestContext)`, then ARRANGE/ACT/ASSERT lines before the existing asserts; PHASE scopes around the clear-TLS calls in `SslStreamConnectionClearTlsTests`.
- Machine-reading tests (`SystemDnsServers`, `SystemNetworkInterfaceLookup`, `SystemClientCertificateStore`, the SSPI default) print only true/false facts, never server lists, interface names or OS text.
- A few asserts that tested an inline expression now test a local holding the same value, so it can be logged; no check changed.
- Counts in the 16 files, before -> after: `Assert.` 152 -> 152, `[TestMethod` 97 -> 97, `[DataRow(` 120 -> 120.
- The acceptance filter ran 197 tests (195 passed, 2 skipped by OS condition); 195 `END` lines, none with a zero count. Project fast tests: 3033 passed, 28 skipped, 0 failed.
- No test printed a `SLOW:` line; the longest PHASE was `clear TLS and send PWD: 52 ms`.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Networking.UnitTests' Q to Sy tests outside SslStreamTlsProviderTests write ARRANGE, ACT and ASSERT diagnostics
