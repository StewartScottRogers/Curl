---
id: BL-254
title: Make the WithoutClientCertificate_PresentsNone TLS test deterministic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-254 — Make the WithoutClientCertificate_PresentsNone TLS test deterministic

## Goal

`SslStreamTlsProviderTests.AuthenticateAsClientAsync_WithoutClientCertificate_PresentsNone`
(both data rows) passes every time, including under `Measure-CodeQuality.ps1`'s coverage
run with other dark factory lanes testing on the same machine, and the reason it failed
is written down.

## Context

- Test: `Curl.Networking.UnitTests\SslStreamTlsProviderTests.ClientCertificate.cs`, the
  `[DataRow(SchannelBuild)]` / `[DataRow(OpenSslBuild)]` test at the top of the file. It
  runs a server-side `SslStream` (`ClientCertificateRequired = true`) over an
  `InMemoryDuplexStream` pair against `SslStreamTlsProvider` (in
  `Curl.Networking.UnitLibrary`), and records in `received` whatever certificate the
  server's validation callback sees. No socket or port is involved, so a shared-port
  explanation is unlikely.
- Observed 2026-09-26 on lane 4 (while working BL-161), and earlier during BL-187: both
  rows failed with `Assert.IsNull failed. 'value' expression: 'handshake.Received'` in two
  consecutive `Measure-CodeQuality.ps1` runs (which run
  `dotnet test --filter "TestCategory!=Integration"` with coverage collection), yet
  passed in a plain `dotnet test --filter "TestCategory!=Integration"` run a minute
  earlier on the same commit. Other lanes were running tests concurrently.
- The failure means the server *did* receive a certificate when `TlsClientOptions` named
  none. Hypothesis to confirm or refute first, not a finding: the other tests in the
  same partial class present `s_clientCertificate` to the same `CertificateHost`, and the
  platform TLS stack (Schannel's credential/session cache on Windows) resumes or reuses a
  session that carries that client certificate, so the result depends on test ordering
  and timing, which the coverage collector and method-level parallelism
  (`MSTestSettings.cs`) change. Other candidates: `SslStream` selecting a certificate
  from the user store automatically, or a race on the captured `received` variable.
- If the root cause is that `SslStreamTlsProvider` can present a certificate the user
  did not ask for (real curl presents none without `--cert`), that is a production bug:
  fix it in `Curl.Networking.UnitLibrary` with a failing test first
  (`.claude/rules/testing.md`). Otherwise fix only the test (for example a distinct
  target host per handshake, or disabling resumption on the test server) without
  weakening what it asserts.
- Quality gates in root `CLAUDE.md` apply to `Curl.Networking.UnitLibrary`: 100% line and
  branch coverage, complexity at most 10, CRAP at most 30. Base class library only.

## Acceptance criteria

- [ ] `## Notes` in this task records the confirmed root cause and the evidence for it
      (a reproduction, not only the hypothesis above).
- [ ] `AuthenticateAsClientAsync_WithoutClientCertificate_PresentsNone` still asserts
      `CurlExitCode.Ok` and that the server received no certificate, for both
      `SchannelBuild` and `OpenSslBuild`; the assertion is not removed, loosened or
      marked `Ignore`/`Integration`.
- [ ] The test passes in 20 consecutive
      `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`
      runs; the run count and result are recorded in `## Notes`.
- [ ] No production behaviour change in `Curl.Networking.UnitLibrary` unless the root
      cause is a production bug; if it is, a new test that fails before the fix and
      passes after it exists in `Curl.Networking.UnitTests` and is named in `## Notes`.
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line
      and branch coverage and no failing member.

## Notes

## Log

- 2026-09-26: Created.
