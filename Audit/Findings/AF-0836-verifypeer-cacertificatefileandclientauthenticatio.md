---
id: AF-0836
title: VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0836 - VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:108`: VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60.

## Evidence

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:108`

The test's only assertion is line 108: Assert.AreEqual(UnsuitablePurpose, failure?.Message);. VerifyPeer returns (CurlExitCode ExitCode, string Message)?, and the exit code the name promises (60) is never asserted.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs -Pattern 'PeerFailedVerification, failure\?\.ExitCode'
```

- Expected: A match inside the test at lines 94-109.
- Actual: Matches only at lines 46 and 64.

## Re-audits

## Log

- 2026-11-17: filed proposed.
