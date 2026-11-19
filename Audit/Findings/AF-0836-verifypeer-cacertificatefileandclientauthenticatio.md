---
id: AF-0836
title: VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
key: quality:Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose:name-lies
reproduction: none
task: BL-5356
tasks: BL-5356
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0134.md
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

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the Select-String. VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose (line 95) now asserts line 109 Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);.
- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Select-String finds the exit-60 assertion at line 109, inside VerifyPeer_CaCertificateFileAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose (line 95), followed by Assert.AreEqual(UnsuitablePurpose, failure?.Message).

## Log

- 2026-11-17: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
