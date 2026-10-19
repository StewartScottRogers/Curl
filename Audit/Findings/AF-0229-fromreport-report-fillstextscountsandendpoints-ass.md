---
id: AF-0229
title: FromReport_Report_FillsTextsCountsAndEndPoints asserts nothing; its checks are TestDiagnostics.Assert log lines
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1905.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-18_1045.md, 2026-10-18_1905.md).
key: quality:Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:FromReport_Report_FillsTextsCountsAndEndPoints:no-assertion
reproduction: none
task: BL-2566
tasks: BL-2566
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed: 2026-10-18
closed-how: consecutive
closed-by: 2026-10-18_1045.md, 2026-10-18_1905.md
---
# AF-0229 - FromReport_Report_FillsTextsCountsAndEndPoints asserts nothing; its checks are TestDiagnostics.Assert log lines

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:19`: FromReport_Report_FillsTextsCountsAndEndPoints asserts nothing; its checks are TestDiagnostics.Assert log lines. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:19`

Lines 42-46 are the test's only checks: diagnostics.Assert("effective url", ...), ("method", ...), ("codes", "201 200 1 1 1 18", ...), ("sizes", ...), ("end points", ...). TestDiagnostics.Assert never throws (TestDiagnostics.cs:189-193), so a FromReport that dropped the response code, the proxy CONNECT code, the sizes or the end points would pass. The file's only Assert calls are the three Assert.ThrowsExactly at lines 70-72 in another test. Found by a tree-wide scan for tests whose only checks go through TestDiagnostics.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/WriteOutTransferInfoTests.cs -Pattern 'Assert\.' | Select-Object LineNumber
```

- Expected: Assert.* lines between 42 and 46, in FromReport_Report_FillsTextsCountsAndEndPoints.
- Actual: Only lines 70, 71 and 72 (Assert.ThrowsExactly in FromReport_NullArgument_Throws).

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: no | Ran the reproduction: WriteOutTransferInfoTests.cs has Assert lines 50-54 inside FromReport_Report_FillsTextsCountsAndEndPoints (AreEqual on effective URL, method, codes, sizes, end points).
- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the Select-String: FromReport_Report_FillsTextsCountsAndEndPoints now has Assert.AreEqual at lines 50-54 (effective URL, method, codes, sizes, end points).

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-18_1045.md, 2026-10-18_1905.md).
