---
id: BL-5356
title: Fix AF-0836: VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5356 — Fix AF-0836: VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60

## Goal

The defect the audit office reported as AF-0836 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0836 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:108`

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:108`

The test's only assertion is line 108: Assert.AreEqual(UnsuitablePurpose, failure?.Message);. VerifyPeer returns (CurlExitCode ExitCode, string Message)?, and the exit code the name promises (60) is never asserted.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs -Pattern 'PeerFailedVerification, failure\?\.ExitCode'
```

- Expected: A match inside the test at lines 94-109.
- Actual: Matches only at lines 46 and 64.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
