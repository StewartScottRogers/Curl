---
id: AF-0661
title: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:Ioctl-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103:==
task: BL-4843
tasks: BL-4843
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_1442.md
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

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the -Site command: resolvedLine 103, mutated 'if (command != IoctlCommand.RestartRead)', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
