---
id: BL-5233
title: Fix AF-0782: ResolveAsync_ServerPortUnreachable_...SoCurlExitsSixInsteadOfSeven never asserts an exit code
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.EncryptedDns.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5233 — Fix AF-0782: ResolveAsync_ServerPortUnreachable_...SoCurlExitsSixInsteadOfSeven never asserts an exit code

## Goal

The defect the audit office reported as AF-0782 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0782 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs:93`

Location: `Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs:93`

Step 1. The name promises curl exits 6 rather than 7; the body asserts only Assert.IsEmpty(addresses); Assert.IsTrue(channel.Refused);. No exit code 6 or 7 is checked anywhere in the test. The scanner missed it because the numbers are spelled out.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.EncryptedDns.UnitTests/DnsOverQuic/DnsOverQuicResolverTests.cs -Pattern 'SoCurlExitsSixInsteadOfSeven' -Context 0,8
```

- Expected: An assertion of exit 6 (CouldntResolveHost).
- Actual: Only Assert.IsEmpty(addresses) and Assert.IsTrue(channel.Refused).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now feeds the refused DoQ resolver to a real TcpConnector (fake dialer that throws) and asserts exit 6 (CouldntResolveHost), not 7.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
