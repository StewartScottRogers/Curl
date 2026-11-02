---
id: AF-0519
title: Lib575_DownloadsThreeTimes counts the performs but never asserts the count
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-01_1801.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs:Lib575_DownloadsThreeTimes:name-lies
reproduction: none
task: BL-4454
tasks: BL-4454
found: 2026-11-01
found-at: 4c39311e3e4524ac6950cb2cba36f5b3f2a6ff2e
scorecard: 2026-11-01_1016.md
duplicate-of:
closed: 2026-11-01
closed-how: reliable-reaudit
closed-by: 2026-11-01_1801.md
---
# AF-0519 - Lib575_DownloadsThreeTimes counts the performs but never asserts the count

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs:417`: Lib575_DownloadsThreeTimes counts the performs but never asserts the count. Reported by an auditor flagged unreliable in 2026-11-01_1016.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs:417`

The body increments 'performs' in the fake engine's Performing callback but asserts only result.ExitCode, ProgramName and a null-argument throw. Nothing checks that three downloads happened. The Lib574 test just above it asserts Assert.AreEqual(performCount, performs). A Lib575 that performed once, or stopped early with the same code, would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs -Pattern 'Lib575_DownloadsThreeTimes' -Context 0,12
```

- Expected: An assertion on performs (3 on the success row).
- Actual: Asserts only Assert.AreEqual(exitCode, result.ExitCode), the program name and ThrowsExactly<ArgumentNullException>; performs is never read.

## Re-audits

- 2026-11-01 | 2026-11-01_1801.md | reproduces: no | Lib575_DownloadsThreeTimes now asserts Assert.AreEqual(performCount, performs) (Lib567To576Tests.cs:428).

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
- 2026-11-01: accepted -> closed. Re-audit 2026-11-01_1801.md: the reproduction no longer reproduces.
