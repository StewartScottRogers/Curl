---
id: AF-0317
title: DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/DnsServerQuery.cs:FoldAsciiLetter-le:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DnsServerQuery.cs:143:<=
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0317 - DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DnsServerQuery.cs:143`: DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected.

## Evidence

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:143`

Sampled mutant (seed 0) survived: 'value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value | 0x20) : value' with <= turned into <. FoldAsciiLetter feeds AsciiEqualsIgnoringCase, which line 68 uses to match a reply's question section against the query. Under the mutant, 'Z' is not folded, so a reply that echoes a name with 'z' for 'Z' (or the reverse) no longer matches and is dropped. The lookup then times out or fails with exit 6. No test pins case-insensitive matching on the last letter.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerQuery.cs:143:<= -Member FoldAsciiLetter -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
