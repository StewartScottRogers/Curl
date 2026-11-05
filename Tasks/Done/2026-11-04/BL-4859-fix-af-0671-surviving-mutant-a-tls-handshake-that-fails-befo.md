---
id: BL-4859
title: Fix AF-0671: Surviving mutant: a TLS handshake that fails before the certificate can be reported as certificate-verified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/PeerVerificationTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `PeerVerificationTests.Unobserved_HandshakeNeverReachedCertificate_ClaimsNoVerifiedCertificate`: `PeerVerification.Unobserved` (the observation `SslStreamTlsProvider` hands `ReportFailedHandshake` when the handshake fails before the certificate callback, and `HandBuiltCertificateVerifier.Observed`'s default) must not be verified, carry no verify result and no chain. No production code changed, so no Measure-CodeQuality run was needed.
- `touches` widened to `Curl.Networking.UnitTests/PeerVerificationTests.cs`, where the test belongs; no task in Doing on work/dark-factory names it.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was checked by hand: with line 15 changed to `new(true, null, [])`, the new test failed (1 of 4064), so the mutant is killed; the value was then put back. The re-audit runs the tool itself.
- Build clean (0 warnings) and the fast tests green on Windows; the test is platform-neutral (no paths, sockets or certificates).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A test now kills AF-0671's mutant: PeerVerification.Unobserved is pinned as not verified
