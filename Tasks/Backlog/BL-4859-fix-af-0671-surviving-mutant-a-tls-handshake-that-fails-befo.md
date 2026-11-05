---
id: BL-4859
title: Fix AF-0671: Surviving mutant: a TLS handshake that fails before the certificate can be reported as certificate-verified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4859 — Fix AF-0671: Surviving mutant: a TLS handshake that fails before the certificate can be reported as certificate-verified

## Goal

The defect the audit office reported as AF-0671 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0671 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0671-surviving-mutant-a-tls-handshake-that-fails-before.md`.

Location: `Curl.Networking.UnitLibrary/PeerVerification.cs:15`

Location: `Curl.Networking.UnitLibrary/PeerVerification.cs:15`

Mutant 'Unobserved { get; } = new(false, null, []);' -> 'new(true, null, [])' survived (sample -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 413592). SslStreamTlsProvider starts with peerVerification = PeerVerification.Unobserved (line 435) and passes it to ReportFailedHandshake when the handshake fails before RemoteCertificateValidationCallback runs (line 496). There CertificateVerified = peerVerification.Verified (line 662), so the handshake event claims a verified certificate that was never seen. HandBuiltCertificateVerifier.Observed defaults to it as well.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/PeerVerification.cs:15:false -Member Unobserved -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
