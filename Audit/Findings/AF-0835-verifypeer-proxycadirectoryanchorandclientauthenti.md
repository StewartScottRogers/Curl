---
id: AF-0835
title: VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose:name-lies
reproduction: none
task: BL-5355
tasks: BL-5355
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0835 - VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:77`: VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose never checks exit 60.

## Evidence

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs:77`

The test's only assertion is line 77: Assert.AreEqual(UnsuitablePurpose, failure?.Message);. Verify returns (CurlExitCode ExitCode, string Message)?, and failure?.ExitCode is never asserted. The sibling tests at lines 46 and 64 do assert Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode). A wrong exit code with the right text would pass. Found by Find-WeakTests (name-lies); no quoted assertion checks 60.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs -Pattern 'PeerFailedVerification, failure\?\.ExitCode'
```

- Expected: Four matches: each IsExit60 test asserts the exit code.
- Actual: Two matches (lines 46 and 64); the tests at lines 69 and 94 assert only the message.

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the Select-String. VerifyPeer_ProxyCaDirectoryAnchorAndClientAuthenticationLeaf_IsExit60UnsuitablePurpose (line 69) now asserts line 77 Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode); beside the message check at 78.

## Log

- 2026-11-17: filed proposed.
