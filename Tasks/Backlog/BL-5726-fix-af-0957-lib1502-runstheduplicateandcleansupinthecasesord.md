---
id: BL-5726
title: Fix AF-0957: Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder never checks the cleanup order its name promises
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5726 — Fix AF-0957: Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder never checks the cleanup order its name promises

## Goal

The defect the audit office reported as AF-0957 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0957 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:289`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:289`

DataRows 1502 to 1505. Lib1502.cs:13-14 and 75-87 give each case its own order (1502 multi then easy, 1503 remove/multi/easy, 1504 easy then multi, 1505 remove/easy/multi). The test asserts only Assert.AreEqual(0, result.ExitCode), the added handle's URL, an empty stderr and ProgramName. Nothing records or compares the order of removal and cleanup, and no other test does (grep for RemoveAndOrder, CleansUpEasyFirst, RemovesFirst in the tests: none). Swapping any two cases' order would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs -Pattern 'Lib1502_RunsTheDuplicateAndCleansUpInTheCasesOrder' -Context 0,12
```

- Expected: The body asserts the removal and cleanup sequence for each test number
- Actual: Its only assertions are the exit code 0, the URL, an empty stderr and the program name

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
