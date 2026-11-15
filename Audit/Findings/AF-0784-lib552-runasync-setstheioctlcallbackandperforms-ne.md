---
id: AF-0784
title: Lib552 RunAsync_SetsTheIoctlCallbackAndPerforms never checks the ioctl callback is set
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:RunAsync_SetsTheIoctlCallbackAndPerforms:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0784 - Lib552 RunAsync_SetsTheIoctlCallbackAndPerforms never checks the ioctl callback is set

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:420`: Lib552 RunAsync_SetsTheIoctlCallbackAndPerforms never checks the ioctl callback is set.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs:420`

Step 1 sample. Body: Assert.AreEqual((int)CurlCode.Ok, result.ExitCode); Assert.AreEqual("lib552", ...ProgramName);. The default FakeCurlEngine performs Ok whatever the options, so dropping CURLOPT_IOCTLFUNCTION (Lib552.cs:62) would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs -Pattern 'RunAsync_SetsTheIoctlCallbackAndPerforms' -Context 0,8
```

- Expected: An assertion on the options the engine received (Callbacks ioctl set).
- Actual: Only the exit code Ok and the program name are asserted.

## Re-audits

## Log

- 2026-11-15: filed proposed.
