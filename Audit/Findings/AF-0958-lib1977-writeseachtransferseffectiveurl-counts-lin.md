---
id: AF-0958
title: Lib1977_WritesEachTransfersEffectiveUrl counts lines starting 'effective URL: ' but never checks a URL
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:Lib1977_WritesEachTransfersEffectiveUrl:weak-assertion
reproduction: none
task: BL-5727
tasks: BL-5727
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0958 - Lib1977_WritesEachTransfersEffectiveUrl counts lines starting 'effective URL: ' but never checks a URL

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:198`: Lib1977_WritesEachTransfersEffectiveUrl counts lines starting 'effective URL: ' but never checks a URL. Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:198`

The only check of the output is Assert.AreEqual(3, run.OutputText.Split('\n', ...).Count(line => line.StartsWith("effective URL: "))). The name says each transfer's effective URL is written (Lib1977.cs:76 writes $"effective URL: {effective}\n"), but a wrong or empty URL after the prefix passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs -Pattern 'StartsWith\("effective URL: "'
```

- Expected: No match: the test compares the whole output, URLs included
- Actual: Lib1964To1977Tests.cs:198 counts prefixed lines only

## Re-audits

## Log

- 2026-11-21: filed proposed.
