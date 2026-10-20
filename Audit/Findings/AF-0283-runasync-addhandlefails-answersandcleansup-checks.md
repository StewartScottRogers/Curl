---
id: AF-0283
title: RunAsync_AddHandleFails_AnswersAndCleansUp checks neither the answer's code nor the clean-up
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:RunAsync_AddHandleFails_AnswersAndCleansUp:weak-assertion
reproduction: none
task: BL-2810
tasks: BL-2810
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: reliable-reaudit
closed-by: 2026-10-19_2134.md
---
# AF-0283 - RunAsync_AddHandleFails_AnswersAndCleansUp checks neither the answer's code nor the clean-up

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:143`: RunAsync_AddHandleFails_AnswersAndCleansUp checks neither the answer's code nor the clean-up. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:143`

With curl_multi_add_handle scripted to answer BadEasyHandle, the only assertion is Assert.AreNotEqual(0, result.ExitCode). The name promises an answer and a clean-up, but no handle disposal or removal is checked, and any non-zero code passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs -Pattern 'RunAsync_AddHandleFails_AnswersAndCleansUp' -Context 0,10
```

- Expected: Asserts the exact exit code and that the easy and multi handles were cleaned up.
- Actual: One Assert.AreNotEqual(0, result.ExitCode) only.

## Re-audits

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | RunAsync_AddHandleFails_AnswersAndCleansUp (Lib539To553Tests.cs:135-145) now asserts the exit code LibTestErrorCodes.Multi, engine.DisposedEasyCount == 1 (clean-up) and engine.Multi.Removals == 0.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
