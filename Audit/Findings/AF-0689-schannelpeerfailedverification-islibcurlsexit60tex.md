---
id: AF-0689
title: SchannelPeerFailedVerification_..._IsLibcurlsExit60Text never asserts exit 60
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/TlsFailureMessagesTests.cs:SchannelPeerFailedVerification_WithOnlyANameMismatchOnAnIpAddressAgainstAlternativeNames_IsLibcurlsExit60Text:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0689 - SchannelPeerFailedVerification_..._IsLibcurlsExit60Text never asserts exit 60

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs:407`: SchannelPeerFailedVerification_..._IsLibcurlsExit60Text never asserts exit 60.

## Evidence

Location: `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs:407`

The name states exit 60. The only assertion is Assert.AreEqual("SSL peer certificate or SSH remote key was not OK", message). Nothing checks 60 or CurlExitCode.PeerFailedVerification, so the name promises an exit code the test never ties to this message.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TlsFailureMessagesTests.cs -Pattern 'IsLibcurlsExit60Text' -Context 0,16
```

- Expected: The test body asserts CurlExitCode.PeerFailedVerification (60).
- Actual: The body's only assertion is Assert.AreEqual("SSL peer certificate or SSH remote key was not OK", message);

## Re-audits

## Log

- 2026-11-05: filed proposed.
