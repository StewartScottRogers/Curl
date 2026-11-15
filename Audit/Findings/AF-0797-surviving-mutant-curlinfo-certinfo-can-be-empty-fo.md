---
id: AF-0797
title: Surviving mutant: CURLINFO_CERTINFO can be empty for certificate text that starts with its PEM block
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:CertInfoRecordsOf-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:34:<
task: BL-5248
tasks: BL-5248
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0797 - Surviving mutant: CURLINFO_CERTINFO can be empty for certificate text that starts with its PEM block

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:34`: Surviving mutant: CURLINFO_CERTINFO can be empty for certificate text that starts with its PEM block.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:34`

Mutant 'if (pemStart < 0)' -> 'if (pemStart <= 0)' in CertInfoRecordsOf survived (seed 5851). With the PEM block at offset 0 (no label lines before it), the mutant returns no records instead of the 'Cert:' record, so a certinfo reader sees nothing for that certificate. The tests only cover text with label lines first, or none at all.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs:34:< -Member CertInfoRecordsOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
