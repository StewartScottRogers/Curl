---
id: AF-0712
title: TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines checks only that two lines come back
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs:TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0712 - TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines checks only that two lines come back

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs:67`: TlsHandshake_SchannelWithoutAHashPin_PrintsOnlyTheAlpnLines checks only that two lines come back. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs:67`

Step 1. The only assertion is Assert.HasCount(2, lines). The name promises the two lines are curl's ALPN lines, but any two lines (wrong ALPN text, a public-key-hash line in place of one) would pass. Diagnostics.Assert("line count", 2, ...) only writes a log line.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/TransferEventInfoTextPinnedPublicKeyTests.cs -Pattern 'PrintsOnlyTheAlpnLines' -Context 0,9
```

- Expected: CollectionAssert.AreEqual of the two ALPN lines curl's Schannel build prints.
- Actual: Assert.HasCount(2, lines); is the only check.

## Re-audits

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
