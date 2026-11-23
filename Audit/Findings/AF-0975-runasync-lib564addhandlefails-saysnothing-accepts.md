---
id: AF-0975
title: RunAsync_Lib564AddHandleFails_SaysNothing accepts any non-zero exit and checks only that one string is absent
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:RunAsync_Lib564AddHandleFails_SaysNothing:weak-assertion
reproduction: none
task: BL-5772
tasks: BL-5772
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1817.md
---
# AF-0975 - RunAsync_Lib564AddHandleFails_SaysNothing accepts any non-zero exit and checks only that one string is absent

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:300`: RunAsync_Lib564AddHandleFails_SaysNothing accepts any non-zero exit and checks only that one string is absent.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:300`

Step 1, LibTest sample. The assertions are Assert.AreNotEqual(0, result.ExitCode); and Assert.IsFalse(run.Error.ToString().Contains("Start at URL", ...));. The name promises that nothing is said. Any other text on stderr, and any non-zero exit code (the wrong one included), passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs -Pattern 'Lib564AddHandleFails_SaysNothing' -Context 0,10
```

- Expected: The test asserts empty stderr (Assert.AreEqual(string.Empty, run.Error.ToString())) and the exact exit code.
- Actual: Assert.AreNotEqual(0, result.ExitCode); and a check that "Start at URL" is absent.

## Re-audits

- 2026-11-22 | 2026-11-22_1817.md | reproduces: no | Ran the Select-String reproduction: no match for Lib564AddHandleFails_SaysNothing. The test is now RunAsync_Lib564AddHandleFails_ReportsOnlyTheFailedCallAndExits32, which asserts Assert.AreEqual(32, result.ExitCode) and the exact standard error 'Lib564.cs:67 curl_multi_add_handle() failed, with code 2 (Invalid easy handle)\n'.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces.
