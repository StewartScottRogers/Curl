---
id: AF-0957
title: Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder never checks the cleanup order its name promises
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0957 - Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder never checks the cleanup order its name promises

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:289`: Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder never checks the cleanup order its name promises. Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:289`

DataRows 1502 to 1505. Lib1502.cs:13-14 and 75-87 give each case its own order (1502 multi then easy, 1503 remove/multi/easy, 1504 easy then multi, 1505 remove/easy/multi). The test asserts only Assert.AreEqual(0, result.ExitCode), the added handle's URL, an empty stderr and ProgramName. Nothing records or compares the order of removal and cleanup, and no other test does (grep for RemoveAndOrder, CleansUpEasyFirst, RemovesFirst in the tests: none). Swapping any two cases' order would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs -Pattern 'Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder' -Context 0,12
```

- Expected: The body asserts the removal and cleanup sequence for each test number
- Actual: Its only assertions are the exit code 0, the URL, an empty stderr and the program name

## Re-audits

## Log

- 2026-11-21: filed proposed.
