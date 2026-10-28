---
id: BL-3872
title: Fix flaky MtlsTestCertificates: File.Copy of test-ca.prm fails 'being used by another process' in the solution-wide fast run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/MtlsTestCertificates.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3872 — Fix flaky MtlsTestCertificates: File.Copy of test-ca.prm fails 'being used by another process' in the solution-wide fast run

## Goal

The Mtls tests in `Curl.Conformance.UnitTests` pass in every solution-wide fast run, not only when the project runs alone.

## Context

Found by BL-3621 (lane 7, 2026-10-26). In `dotnet test --filter "TestCategory!=Integration"` over the whole solution, four tests failed (`HttpsServerConnectorTests.ConnectAsync_MtlsPortWithACertificateTheTestCaDidNotSign_FailsTheHandshakeAndRecordsIt`, `ConnectAsync_MtlsPortWithTheEkuOnlyClientCertificate_SucceedsAndRecordsIt`, `ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake`, `UpstreamHttpsMtlsFrontTests.Wrap_CaseNamingTheHttpsMtlsServer_PutsTheMtlsServerInFront`) with `System.IO.IOException: The process cannot access the file '...\Curl.Conformance.UnitTests\bin\Debug\net10.0\MtlsCertificates\source\test-ca.prm' because it is being used by another process`, thrown by `File.Copy(..., overwrite: true)` in `MtlsTestCertificates.cs:13`. Run alone (`--filter "FullyQualifiedName~Mtls"`) all 11 Mtls tests passed. Another process (likely another test host sharing that output folder, such as the Integration project or a second assembly copying the same folder) holds the file; a per-process unique folder (for example under a temporary directory the test creates and deletes) or a copy that tolerates an identical existing file would remove the race.

## Acceptance criteria

- [x] `MtlsTestCertificates` no longer overwrites a shared file another process may hold open (each process generates into its own folder, or skips a copy whose bytes already match).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

Each process now generates into its own folder (MtlsCertificates/process-<pid>-<guid>), so no shared file is overwritten. Platform-neutral; fast tests green for the Mtls tests here, solution-wide run left to the shift.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d362e868 779790c0.
- 2026-10-28: Doing -> Done. Per-process certificate folder
