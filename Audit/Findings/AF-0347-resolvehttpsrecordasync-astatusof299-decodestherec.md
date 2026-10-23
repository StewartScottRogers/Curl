---
id: AF-0347
title: ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord checks only that a record is not null
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs:ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord:weak-assertion
reproduction: none
task: BL-3127
tasks: BL-3127
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0347 - ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord checks only that a record is not null

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs:239`: ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord checks only that a record is not null.

## Evidence

Location: `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs:239`

The only assertion is Assert.IsNotNull(record). The name promises the record is decoded, and the answer is a known AlpnAndEchRecord, but neither its ALPN list, its port nor its ECH config is compared. A decoder that returned an empty or wrong record on a 299 would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs -Pattern 'AStatusOf299_DecodesTheRecord' -Context 0,10
```

- Expected: The record's fields are compared with AlpnAndEchRecord's.
- Actual: Assert.IsNotNull(record); is the only check.

## Re-audits

- 2026-10-22 | 2026-10-22_1902.md | reproduces: no | Ran the Select-String reproduction: ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord now also asserts record.Priority == 1 and ApplicationProtocols == ["h2"] (lines 248-249).

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
