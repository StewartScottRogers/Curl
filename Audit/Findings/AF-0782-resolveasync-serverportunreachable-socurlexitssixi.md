---
id: AF-0782
title: ResolveAsync_ServerPortUnreachable_...SoCurlExitsSixInsteadOfSeven never asserts an exit code
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs:ResolveAsync_ServerPortUnreachable_FindsNoAddressesSoCurlExitsSixInsteadOfSeven:name-lies
reproduction: none
task: BL-5233
tasks: BL-5233
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0782 - ResolveAsync_ServerPortUnreachable_...SoCurlExitsSixInsteadOfSeven never asserts an exit code

## Summary

Medium finding from the quality auditor at `Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs:93`: ResolveAsync_ServerPortUnreachable_...SoCurlExitsSixInsteadOfSeven never asserts an exit code.

## Evidence

Location: `Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs:93`

Step 1. The name promises curl exits 6 rather than 7; the body asserts only Assert.IsEmpty(addresses); Assert.IsTrue(channel.Refused);. No exit code 6 or 7 is checked anywhere in the test. The scanner missed it because the numbers are spelled out.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs -Pattern 'SoCurlExitsSixInsteadOfSeven' -Context 0,8
```

- Expected: An assertion of exit 6 (CouldntResolveHost).
- Actual: Only Assert.IsEmpty(addresses) and Assert.IsTrue(channel.Refused).

## Re-audits

## Log

- 2026-11-15: filed proposed.
