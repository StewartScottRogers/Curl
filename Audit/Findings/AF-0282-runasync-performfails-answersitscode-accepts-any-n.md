---
id: AF-0282
title: RunAsync_PerformFails_AnswersItsCode accepts any non-zero exit code instead of the code it names
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:RunAsync_PerformFails_AnswersItsCode:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0282 - RunAsync_PerformFails_AnswersItsCode accepts any non-zero exit code instead of the code it names

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:103`: RunAsync_PerformFails_AnswersItsCode accepts any non-zero exit code instead of the code it names. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:103`

With curl_multi_perform scripted to answer CurlMultiCode.BadHandle, the test checks only Assert.AreNotEqual(0, result.ExitCode). The name promises 'its code', but any failure code passes, MajorBad from a wrong branch included. Lib540 is the same pattern as AF-0247.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs -Pattern 'Assert.AreNotEqual\(0, result.ExitCode\)'
```

- Expected: The test asserts the exact exit code lib540 answers for a failed multi perform.
- Actual: Lib539To553Tests.cs:103 (and :143) assert only AreNotEqual(0, result.ExitCode).

## Re-audits

## Log

- 2026-10-18: filed proposed.
