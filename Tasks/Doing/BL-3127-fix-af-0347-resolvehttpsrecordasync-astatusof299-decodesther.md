---
id: BL-3127
title: Fix AF-0347: ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord checks only that a record is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-22
completed:
---
# BL-3127 — Fix AF-0347: ResolveHttpsRecordAsync_AStatusOf299_DecodesTheRecord checks only that a record is not null

## Goal

The defect the audit office reported as AF-0347 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0347 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0347-resolvehttpsrecordasync-astatusof299-decodestherec.md`.

Location: `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs:239`

Location: `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs:239`

The only assertion is Assert.IsNotNull(record). The name promises the record is decoded, and the answer is a known AlpnAndEchRecord, but neither its ALPN list, its port nor its ECH config is compared. A decoder that returned an empty or wrong record on a 299 would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs -Pattern 'AStatusOf299_DecodesTheRecord' -Context 0,10
```

- Expected: The record's fields are compared with AlpnAndEchRecord's.
- Actual: Assert.IsNotNull(record); is the only check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The test now compares priority, ALPN list, port and ECH config list with AlpnAndEchRecord.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
