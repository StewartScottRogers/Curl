---
id: AF-0446
title: Lib555's RunAsync_SetsTheIoctlCallbackAndPerforms never checks that the ioctl callback is set
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:RunAsync_SetsTheIoctlCallbackAndPerforms:name-lies
reproduction: none
task: BL-3523
tasks: BL-3523
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0446 - Lib555's RunAsync_SetsTheIoctlCallbackAndPerforms never checks that the ioctl callback is set

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:111`: Lib555's RunAsync_SetsTheIoctlCallbackAndPerforms never checks that the ioctl callback is set. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:111`

The body runs Lib555 against a FakeCurlEngine with no Performing hook. It asserts only Assert.AreEqual((int)CurlCode.Ok, result.ExitCode) and the ProgramName "lib555". It never reads options.Callbacks for the Ioctlfunction/Ioctldata that Curl.Conformance.LibTest.UnitLibrary/Lib555.cs:51-52 sets, and no other Lib555 test does either (only this one and a null-argument test exist). Removing the two Ioctl option lines from Lib555 would leave the test green. The twin test for Lib553 (Lib539To553Tests.cs:377) does call the reader's Ioctl.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs -Pattern 'RunAsync_SetsTheIoctlCallbackAndPerforms' -Context 0,8
```

- Expected: The body captures the performed EasyOptions and asserts that its Ioctl callback is the reader's Ioctl (for example that it answers IoctlResult.Ok to RestartRead).
- Actual: The body asserts only exit code 0 and ProgramName "lib555"; it never mentions Ioctl.

## Re-audits

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
