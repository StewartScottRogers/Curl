---
id: BL-297
title: Bring DerCertificateFile.OuterValueEnd and ClientCertificateFileTypeName.Parse under the complexity limit
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-297 — Bring DerCertificateFile.OuterValueEnd and ClientCertificateFileTypeName.Parse under the complexity limit

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` no longer reports
`DerCertificateFile.OuterValueEnd(byte[])` or `ClientCertificateFileTypeName.Parse(string)`
as failing on complexity, with no change in behaviour.

## Context

- Found during BL-254 (2026-09-26). Both members landed with BL-249 (commit 243dbcd).
  `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`
  reports each at Cobertura cyclomatic complexity 14 against the limit of 10, with 100%
  line and branch coverage. The build's CA1502 gate passes them: CA1502 measures the source
  and Cobertura the compiled method, and pattern matching and `switch` expressions compile
  to more branches (see the script's header).
- Files: `Curl.Networking.UnitLibrary\DerCertificateFile.cs` (around line 84) and
  `Curl.Networking.UnitLibrary\ClientCertificateFileType.cs` (around line 36).
- Split each into smaller members (for example a lookup table for the type names, or one
  helper per DER length form) so every resulting member is at most 10. Thresholds in
  `CodeMetricsConfig.txt` must not change.
- The same run also reports `TcpDialer.DialAsync` and `UdpDatagramChannel.SendAsync` /
  `ReceiveAsync` as under-covered: their tests are the loopback `Integration` tests, so they
  are covered only with `-IncludeIntegration`. Not part of this task. `SslStreamTlsProvider.CreateCipherSuitesPolicy`
  is BL-268.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`
      lists neither `DerCertificateFile.OuterValueEnd` nor `ClientCertificateFileTypeName.Parse`
      (nor any member split out of them) as failing.
- [x] The existing `DerCertificateFile` and `ClientCertificateFileTypeName` tests in
      `Curl.Networking.UnitTests` pass unchanged.
- [x] `CodeMetricsConfig.txt` is unchanged; `dotnet build` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered directly rather than through the full `/feature` stages: a behaviour-preserving
  split of two private/internal members, no new seam, no design decision, no ADR needed.
- `DerCertificateFile.OuterValueEnd` now handles the short form and delegates the long form
  to a new `LongFormValueEnd(byte[], int)`.
- `ClientCertificateFileTypeName.Parse` now looks names up in a `FrozenDictionary` with
  `StringComparer.OrdinalIgnoreCase` (null stays PEM, unknown stays Unsupported). Ordinal
  ignore-case uses the same invariant simple case mapping as `ToUpperInvariant`, so the
  accepted names are unchanged.
- Measured after: Curl.Networking.UnitLibrary reports 4 failing members, all the
  out-of-scope ones the Context names (CreateCipherSuitesPolicy is BL-268; TcpDialer and
  UdpDatagramChannel are Integration-only). No existing test changed.


## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. OuterValueEnd and Parse split under complexity 10; Measure-CodeQuality no longer lists them, behaviour unchanged
