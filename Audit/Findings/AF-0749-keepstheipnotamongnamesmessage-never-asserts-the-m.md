---
id: AF-0749
title: ...KeepsTheIpNotAmongNamesMessage never asserts the message it names, only that it is not another one
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs:VerifyServerCertificateChain_SchannelIpTargetAgainstCommonNameLocalhostWithDnsName_KeepsTheIpNotAmongNamesMessage:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0749 - ...KeepsTheIpNotAmongNamesMessage never asserts the message it names, only that it is not another one

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs:301`: ...KeepsTheIpNotAmongNamesMessage never asserts the message it names, only that it is not another one.

## Evidence

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs:301`

The body asserts Assert.IsFalse(verdict.IsAccepted); and Assert.AreNotEqual("schannel: CertFindExtension() returned no extension.", verdict.ErrorMessage);. The name promises the IP-not-among-names message is kept, but any other wrong message (empty text, a different curl error) passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs -Pattern 'KeepsTheIpNotAmongNamesMessage' -Context 0,10
```

- Expected: Assert.AreEqual of the exact IP-not-among-names message.
- Actual: Only Assert.AreNotEqual against a different message.

## Re-audits

## Log

- 2026-11-14: filed proposed.
