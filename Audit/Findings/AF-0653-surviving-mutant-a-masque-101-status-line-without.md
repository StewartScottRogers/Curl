---
id: AF-0653
title: Surviving mutant: a MASQUE 101 status line without a reason phrase can be refused as malformed
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:ParseStatus-lt:surviving-mutant
reproduction: mutation Curl.Extension.Masque.UnitLibrary/Wire/MasqueUpgrade.cs:61:<
task: BL-4835
tasks: BL-4835
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_1442.md
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

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the -Site command: resolvedLine 61, mutated 'parts.Length <= 2 || ...', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
