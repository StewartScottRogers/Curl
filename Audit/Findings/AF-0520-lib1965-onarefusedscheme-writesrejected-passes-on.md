---
id: AF-0520
title: Lib1965_OnARefusedScheme_WritesRejected passes on empty output
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-11-01 ("accept all").
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:Lib1965_OnARefusedScheme_WritesRejected:weak-assertion
reproduction: none
task: BL-4455
tasks: BL-4455
found: 2026-11-01
found-at: 4c39311e3e4524ac6950cb2cba36f5b3f2a6ff2e
scorecard: 2026-11-01_1016.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0520 - Lib1965_OnARefusedScheme_WritesRejected passes on empty output

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:97`: Lib1965_OnARefusedScheme_WritesRejected passes on empty output. Reported by an auditor flagged unreliable in 2026-11-01_1016.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:97`

The only output check is Assert.IsTrue(run.OutputText.Split('\n', RemoveEmptyEntries).All(line => line.EndsWith(" REJECTED"))). All() is true for no lines, so a Lib1965 that wrote nothing would pass. The neighbouring test asserts Assert.HasCount(Lib1965.Schemes.Count + 1, lines).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs -Pattern 'Lib1965_OnARefusedScheme_WritesRejected' -Context 0,8
```

- Expected: A line-count assertion beside the All(...) check.
- Actual: Only Assert.AreEqual(0, result.ExitCode) and Assert.IsTrue(...All(line => line.EndsWith(" REJECTED", ...))).

## Re-audits

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
