---
id: BL-5771
title: Fix AF-0974: Find_Lib509_RunsOverANewCountingAllocator only checks that the exit code is not -1, never that a counting allocator was used
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5771 — Fix AF-0974: Find_Lib509_RunsOverANewCountingAllocator only checks that the exit code is not -1, never that a counting allocator was used

## Goal

The defect the audit office reported as AF-0974 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0974 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs:213`

Location: `Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs:213`

Step 1, LibTest sample. The whole body is: var run = new PortRun(new FakeCurlEngine(), 509, ...); LibTestResult result = await LibTestPrograms.Ported.Find("lib509")!.RunAsync(run.Context, ...); Assert.AreNotEqual(-1, result.ExitCode);. The name promises that lib509 runs over a new counting allocator, but no allocator, count or output is checked. Any exit code other than -1 passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib510To519Tests.cs -Pattern 'RunsOverANewCountingAllocator' -Context 0,7
```

- Expected: The test asserts the allocator lib509 runs over (its counted calls or its output) and an exact exit code.
- Actual: Its only assertion is Assert.AreNotEqual(-1, result.ExitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
