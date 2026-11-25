---
id: AF-0895
title: RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting asserts exit 25
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs:RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting:name-lies
reproduction: none
task: BL-5583
tasks: BL-5583
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
---
# AF-0895 - RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting asserts exit 25

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs:151`: RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting asserts exit 25.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs:151`

The name says Exits26. The body asserts Assert.AreEqual(25, exitCode); and the standard error 'curl: (25) ...'. Nothing checks 26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs -Pattern 'Exits26WithoutConnecting|AreEqual\(25, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 151 names Exits26; line 164 asserts Assert.AreEqual(25, exitCode);

## Re-audits

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the Select-String: only the name line (151) matches; no AreEqual(25, exitCode). The test asserts Assert.AreEqual(26, exitCode).

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
