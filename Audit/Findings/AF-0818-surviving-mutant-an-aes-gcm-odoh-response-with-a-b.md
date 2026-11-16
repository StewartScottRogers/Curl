---
id: AF-0818
title: Surviving mutant: an AES-GCM ODoH response with a bad tag can be reported as opened
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:TryOpen-false:surviving-mutant
reproduction: mutation Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:46:false
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
# AF-0818 - Surviving mutant: an AES-GCM ODoH response with a bad tag can be reported as opened

## Summary

Medium finding from the quality auditor at `Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:46`: Surviving mutant: an AES-GCM ODoH response with a bad tag can be reported as opened.

## Evidence

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:46`

In TryOpen, `return false;` after AuthenticationTagMismatchException changed to `return true;` survived. OpenResponse_ATamperedResponse_GivesNull (default suite Aes128Gcm) still passes, because a later check of the zeroed plaintext happens to reject it. TryOpen's own authentication decision is untested: nothing asserts that TryOpen refuses a tampered tag.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ObliviousDoh.UnitLibrary/Encryption/ResponseAead.cs:46:false -Member TryOpen -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
