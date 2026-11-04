---
id: AF-0653
title: Surviving mutant: a MASQUE 101 status line without a reason phrase can be refused as malformed
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:ParseStatus-lt:surviving-mutant
reproduction: mutation Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61:<
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0653 - Surviving mutant: a MASQUE 101 status line without a reason phrase can be refused as malformed

## Summary

Medium finding from the quality auditor at `Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61`: Surviving mutant: a MASQUE 101 status line without a reason phrase can be refused as malformed.

## Evidence

Location: `Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61`

Mutant 'parts.Length < 2' -> 'parts.Length <= 2' survived. statusLine.Split(' ', 3) of 'HTTP/1.1 101' gives 2 parts, so the mutant refuses a valid upgrade reply and the tunnel fails with exit 7. No test sends a status line without a reason phrase.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61:< -Member ParseStatus -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
