---
id: BL-5235
title: Fix AF-0784: Lib552 RunAsync_SetsTheIoctlCallbackAndPerforms never checks the ioctl callback is set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5235 — Fix AF-0784: Lib552 RunAsync_SetsTheIoctlCallbackAndPerforms never checks the ioctl callback is set

## Goal

The defect the audit office reported as AF-0784 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0784 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:420`

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:420`

Step 1 sample. Body: Assert.AreEqual((int)CurlCode.Ok, result.ExitCode); Assert.AreEqual("lib552", ...ProgramName);. The default FakeCurlEngine performs Ok whatever the options, so dropping CURLOPT_IOCTLFUNCTION (Lib552.cs:62) would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs -Pattern 'RunAsync_SetsTheIoctlCallbackAndPerforms' -Context 0,8
```

- Expected: An assertion on the options the engine received (Callbacks ioctl set).
- Actual: Only the exit code Ok and the program name are asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
