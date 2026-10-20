---
id: AF-0282
title: RunAsync_PerformFails_AnswersItsCode accepts any non-zero exit code instead of the code it names
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:RunAsync_PerformFails_AnswersItsCode:weak-assertion
reproduction: none
task: BL-2809
tasks: BL-2809
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: reliable-reaudit
closed-by: 2026-10-19_2134.md
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

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Select-String found no Assert.AreNotEqual(0, result.ExitCode). RunAsync_PerformFails_AnswersItsCode (Lib539To553Tests.cs:95) now asserts Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode).

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
