---
id: AF-0661
title: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:Ioctl-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103:==
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
# AF-0661 - Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103`: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103`

Mutant 'command == IoctlCommand.RestartRead' -> '!=' survived. REWIND! would be written and the upload counter reset on the wrong commands, and not on a real restart. That changes stderr and the bytes uploaded. No test fails.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103:== -Member Ioctl -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
