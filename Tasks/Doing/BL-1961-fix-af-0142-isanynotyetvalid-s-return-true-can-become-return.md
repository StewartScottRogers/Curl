---
id: BL-1961
title: Fix AF-0142: IsAnyNotYetValid's 'return true' can become 'return false' with no test failing: not-yet-valid certificates report 'expired (10)'
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-10
completed:
---
# BL-1961 — Fix AF-0142: IsAnyNotYetValid's 'return true' can become 'return false' with no test failing: not-yet-valid certificates report 'expired (10)'

## Goal

The defect the audit office reported as AF-0142 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0142 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0142-isanynotyetvalid-s-return-true-can-become-return-f.md`.

Location: `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131`

Location: `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived, original 'return true;' mutated 'return false;' in IsAnyNotYetValid. OpenSslVerifyResult.Of passes IsAnyNotYetValid(chain, now) to OfChainStatus, which picks CertificateNotYetValid (9) over CertificateHasExpired (10) only when it is true; ServerCertificateVerification.cs:281 uses Of for the verify result curl's OpenSSL build reports. With the mutant every NotTimeValid chain is reported as expired, so a certificate whose NotBefore is in the future gets 'certificate has expired (10)' instead of 'certificate is not yet valid (9)' in -v/-k output. OpenSslVerifyResultTests covers OfChainStatus with the flag given directly (DataRow at line 102) but no test drives Of with a not-yet-valid chain.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131:true -Member IsAnyNotYetValid -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
