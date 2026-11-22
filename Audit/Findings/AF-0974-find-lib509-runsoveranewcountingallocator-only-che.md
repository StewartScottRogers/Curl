---
id: AF-0974
title: Find_Lib509_RunsOverANewCountingAllocator only checks that the exit code is not -1, never that a counting allocator was used
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs:Find_Lib509_RunsOverANewCountingAllocator:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0974 - Find_Lib509_RunsOverANewCountingAllocator only checks that the exit code is not -1, never that a counting allocator was used

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs:213`: Find_Lib509_RunsOverANewCountingAllocator only checks that the exit code is not -1, never that a counting allocator was used.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs:213`

Step 1, LibTest sample. The whole body is: var run = new PortRun(new FakeCurlEngine(), 509, ...); LibTestResult result = await LibTestPrograms.Ported.Find("lib509")!.RunAsync(run.Context, ...); Assert.AreNotEqual(-1, result.ExitCode);. The name promises that lib509 runs over a new counting allocator, but no allocator, count or output is checked. Any exit code other than -1 passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs -Pattern 'RunsOverANewCountingAllocator' -Context 0,7
```

- Expected: The test asserts the allocator lib509 runs over (its counted calls or its output) and an exact exit code.
- Actual: Its only assertion is Assert.AreNotEqual(-1, result.ExitCode);

## Re-audits

## Log

- 2026-11-22: filed proposed.
