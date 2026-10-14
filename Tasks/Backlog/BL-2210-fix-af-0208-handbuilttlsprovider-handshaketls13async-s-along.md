---
id: BL-2210
title: Fix AF-0208: HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2210 — Fix AF-0208: HandBuiltTlsProvider.HandshakeTls13Async's alongsideTls12: false can become true with no test failing

## Goal

The defect the audit office reported as AF-0208 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0208 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0208-handbuilttlsprovider-handshaketls13async-s-alongsi.md`.

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702`

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 left this mutant surviving: 'settings.ToTls13(alongsideTls12: false)' -> 'settings.ToTls13(alongsideTls12: true)'. The flag's only effect, in ToTls13 at line 814, decides the ClientHello's supported_groups for a TLS 1.3-minimum handshake: false drops every group TLS 1.3 cannot key-share (TlsNamedGroup.CanShare). The profiles' default lists hold only shareable groups, but --curves rewrites the list (CurvesAndSignatureAlgorithms.Apply). So with --tlsv1.3 and a TLS 1.2-only curve such as brainpoolP256r1 (0x001a), the hello would advertise a group it cannot use: different request bytes from the measured profile. AuthenticateAsClientAsync_WithATls13Minimum_OffersOnlyTls13InTheProfilesOrder checks suites, supported_versions and extension order, never supported_groups' content.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:702:false -Member HandshakeTls13Async -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins the TLS 1.3-only hello's supported_groups under --curves.
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
