---
id: AF-0069
title: Requirements FR-007 cites test RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching, which does not exist
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-10-08_2315.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_0748.md, 2026-10-08_2315.md).
key: truthfulness:Documentation/Product/Requirements.md:FR-007:false-statement
reproduction: none
task: BL-1688
tasks: BL-1688
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: consecutive
closed-by: 2026-10-08_0748.md, 2026-10-08_2315.md
---
# AF-0069 - Requirements FR-007 cites test RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching, which does not exist

## Summary

Medium finding from the truthfulness auditor at `Documentation/Product/Requirements.md:40`: Requirements FR-007 cites test RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching, which does not exist. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Documentation/Product/Requirements.md:40`

FR-007 says the range hand-off is 'shown by `CurlCommandRunnerTransferOptionTests.RunAsync_Range_WritesOnlyThoseBytes` and `RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching`'. The second test exists nowhere in the solution. The tests that do exist describe different behaviour: CurlCommandRunnerRangeTextHandOffTests.RunAsync_SilentVerboseFileRangeThatNamesNoRange_ShutsDownTheConnectionAndExits33 (a connection is made and shut down, not 'without dispatching'), and CurlCommandRunnerTransferOptionTests.RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped. An agent checking the requirement's evidence finds nothing, and the 'without dispatching' claim is no longer what the runner does.

## Reproduction

Run from the repository root:

```powershell
(Get-ChildItem -Path Curl.Console.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch 'RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching').Count
```

- Expected: 1 or more: the cited test exists
- Actual: 0 (Requirements.md:40 cites it)

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | The count is still 0: the test does not exist. But FR-007 (Requirements.md:40) no longer cites it. It now cites CurlCommandRunnerRangeTextHandOffTests.RunAsync_SilentVerboseFileRangeThatNamesNoRange_ShutsDownTheConnectionAndExits33 and CurlCommandRunnerTransferOptionTests.RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped, and both exist (CurlCommandRunnerRangeTextHandOffTests.cs:39, CurlCommandRunnerTransferOptionTests.cs:75).
- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | The count is 0, and FR-007 no longer cites that test name. It now cites CurlCommandRunnerRangeTextHandOffTests.RunAsync_SilentVerboseFileRangeThatNamesNoRange_ShutsDownTheConnectionAndExits33 and CurlCommandRunnerTransferOptionTests.RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped, and both exist in Curl.Console.UnitTests.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_2315.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_0748.md, 2026-10-08_2315.md).
