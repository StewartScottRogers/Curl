---
id: AF-0816
title: Surviving mutant: an HTTPS-record target name starting with NUL can be junk-scanned past its C-string end
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs:HasControlCharacter-ge:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs:94:>=
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0816 - Surviving mutant: an HTTPS-record target name starting with NUL can be junk-scanned past its C-string end

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs:94`: Surviving mutant: an HTTPS-record target name starting with NUL can be junk-scanned past its C-string end.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs:94`

`if (end >= 0)` changed to `if (end > 0)` survived. When the target name's first byte is NUL, curl's C string is empty and Curl_junkscan finds nothing. The mutant scans the whole name and can refuse a record curl accepts. No test has a target name beginning with NUL.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs:94:>= -Member HasControlCharacter -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
