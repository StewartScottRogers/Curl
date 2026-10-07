---
id: BL-1543
title: Make SslStreamTlsProviderTests' Alpn to ClientCertificateTypes files write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1543 — Make SslStreamTlsProviderTests' Alpn to ClientCertificateTypes files write descriptive diagnostic output

## Goal

Every test declared in `Curl.Networking.UnitTests`' `SslStreamTlsProviderTests.cs` and its partial files `.Alpn`, `.AutoClientCertificate`, `.ChainErrors`, `.Ciphers`, `.ClientCertificate`, `.ClientCertificateStore` and `.ClientCertificateTypes` (8 files, 132 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs (TLS settings, certificates by subject and thumbprint), Act result and assertion context (plus `PHASE` timings for the TLS handshake), with no test's logic or assertions changed.

## Context

- Split from BL-1468 (one task per range of files, as its Notes direct); BL-1468 keeps the whole-project checks and depends on this task. Follow BL-1468's Context: what matters in this project, `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- `SslStreamTlsProviderTests` is one partial class of 206 tests across 19 files; BL-1544 covers the other 11 files and checks the whole class once this task is Done.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~Curl.Networking.SslStreamTlsProviderTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and no `END` line for a test declared in this task's 8 files matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Approach: `SslStreamTlsProviderTests` gained a `TestContext` and a `Diagnostics` property. The
  shared handshake helpers (`HandshakeAsync`, `HandshakeWithClientCertificateRequestAsync`,
  `HandshakeWithServerCertificateAsync`, `NegotiateAsync`, `AlpnReportingHandshakeAsync`,
  `CaptureHandshakeOptionsAsync`) became instance methods that write the TLS settings, build,
  target host and certificates (subject and thumbprint) as ARRANGE, run the handshake inside a
  `handshake` PHASE and write exit code, error message and received certificate as ACT.
  `AssertPresented` and `AssertFailed` write ASSERT and DIFF lines; each other test writes its
  own ASSERT line beside its first assertion. Platform-skip tests (Windows-only, Linux-only,
  TLS 1.3 probe, CipherSuitesPolicy) write their platform check before `Assert.Inconclusive`.
- Nothing printed depends on the operating system: TLS settings write paths only as set or
  none, and the ACT error message writes the test's temporary folder as `<test folder>`.
- Counts in the 8 files (`Assert.` / `[TestMethod` / `[DataRow(`), before and after, unchanged:
  main 110/50/28, Alpn 15/9/0, AutoClientCertificate 6/4/2, ChainErrors 12/8/2, Ciphers 22/9/12,
  ClientCertificate 18/23/25, ClientCertificateStore 3/7/0, ClientCertificateTypes 0/22/48.
- The class filter ran 318 tests and printed 314 END lines; the 4 without one are
  `AuthenticateAsClientAsync_WithACeilingBelowTls12InTheOpenSslBuildOnLinux_ReportsTheMeasuredLine`
  (VersionRange.cs, BL-1544's range), skipped by `[OSCondition(Linux)]` on Windows before any
  hook runs, so not run. No END line for a test in this task's 8 files has a zero count.
- SLOW: none of the tests printed a `SLOW:` line (slowest handshakes about 0.3 s).
- No library changed, so Measure-CodeQuality was not run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in SslStreamTlsProviderTests' main, Alpn to ClientCertificateTypes files writes ARRANGE, ACT, ASSERT and a handshake PHASE through TestDiagnostics
