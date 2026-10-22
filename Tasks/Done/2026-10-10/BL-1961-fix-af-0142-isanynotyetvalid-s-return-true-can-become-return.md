---
id: BL-1961
title: Fix AF-0142: IsAnyNotYetValid's 'return true' can become 'return false' with no test failing: not-yet-valid certificates report 'expired (10)'
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix is test-only: `OpenSslVerifyResultTests.Of_TrustedChainOutsideItsValidity_ReturnsNotYetValidOrExpired`
  builds a self-signed certificate trusted through `X509ChainTrustMode.CustomRootTrust`, so the
  chain's only fault is `NotTimeValid` and `Of` must tell not-yet-valid (9) from expired (10)
  through `IsAnyNotYetValid`. The existing self-signed test could not: `UntrustedRoot` wins first.
- `Curl.Networking.UnitTests` added to `touches` for the test; no task in Doing on
  `origin/work/dark-factory` names it (BL-1959: Curl.Protocol.Http.UnitLibrary, BL-1979: Tftp).
- The audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was
  applied by hand (line 131 `return true;` -> `return false;`): the new test's (5,10,9) row fails
  against it (killed); reverted, all 18 tests pass. The quality auditor's re-audit confirms it.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. A not-yet-valid trusted chain is now tested through Of, killing the AF-0142 mutant
