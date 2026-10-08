---
id: BL-1639
title: Make Curl.Tls.UnitTests' Tls13 CertificateCompression to ClientHelloBuilder tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1639 — Make Curl.Tls.UnitTests' Tls13 CertificateCompression to ClientHelloBuilder tests write descriptive diagnostic output

## Goal

Every test in `Curl.Tls.UnitTests`' `Tls13CertificateCompressionHandshakeTests`, `Tls13ClientConnectionTests`, `Tls13ClientHandshakeTests` and `Tls13ClientHelloBuilderTests` (4 files, 57 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings for each handshake flight), with no test's logic or assertions changed.

## Context

- Split from BL-1489 (one task per range of files, as its Notes direct); BL-1489 keeps the whole-project checks and depends on this task. Follow BL-1489's Context for what matters in this project, and `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- Every class is in namespace `Curl.Tls`, flat in the project root. A shared fake or helper (`Tls13TestServer`, `HandshakeDriver`) may write lines for the tests that use it, as long as each test's `END` line counts them.

## Acceptance criteria

- [x] `dotnet test Curl.Tls.UnitTests --filter "FullyQualifiedName~Curl.Tls.Tls13CertificateCompressionHandshakeTests.|FullyQualifiedName~Curl.Tls.Tls13ClientConnectionTests.|FullyQualifiedName~Curl.Tls.Tls13ClientHandshakeTests.|FullyQualifiedName~Curl.Tls.Tls13ClientHelloBuilderTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Tls.UnitTests -warnaserror` is clean and `dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Tls.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before -> after: Tls13CertificateCompressionHandshakeTests 17/11/10 -> 17/11/10; Tls13ClientConnectionTests 34/16/2 -> 34/16/2; Tls13ClientHandshakeTests 53/27/49 -> 53/27/49; Tls13ClientHelloBuilderTests 4/3/6 -> 4/3/6. `Diagnostics.Assert(` lines were added beside every MSTest assertion; none was removed or changed.
- The filtered detailed run printed 109 `END` lines for 109 tests, none with a zero ARRANGE, ACT or ASSERT count. Every handshake runs inside a `PHASE handshake` scope (a private `RunTimed`/`ConnectTimedAsync`/`AwaitTimedAsync` wrapper per class; HelloRetryRequest and post-handshake tests time each flight separately). Typical handshake phase 15-20 ms.
- No test printed a `SLOW:` line; no follow-up task needed.
- Shared helpers (`AssertBadCertificate`, `AssertCompletes`) became instance methods so they can write through the test's own `TestContext`; their logic is unchanged.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Tls13 CertificateCompression, ClientConnection, ClientHandshake and ClientHelloBuilder tests write ARRANGE/ACT/ASSERT and PHASE diagnostics
