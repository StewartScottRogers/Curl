---
id: AF-0446
title: Lib555's RunAsync_SetsTheIoctlCallbackAndPerforms never checks that the ioctl callback is set
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
key: quality:Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:RunAsync_SetsTheIoctlCallbackAndPerforms:name-lies
reproduction: none
task: BL-3523
tasks: BL-3523
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed: 2026-10-29
closed-how: consecutive
closed-by: 2026-10-25_1944.md, 2026-10-29_0606.md
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

- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | Lib554To566Tests.cs:119-121 reads the Ioctl callback off the added handle's options, asserts IsNotNull, then calls it: Assert.AreEqual(IoctlResult.Ok, ioctl(IoctlCommand.RestartRead)), and checks the exit code is Ok.
- 2026-10-29 | 2026-10-29_0606.md | reproduces: no | Ran the Select-String reproduction. RunAsync_SetsTheIoctlCallbackAndPerforms (Lib554To566Tests.cs:112) now calls the callback and asserts its answer: `Assert.AreEqual(IoctlResult.Ok, ioctl(IoctlCommand.RestartRead));` (line 121), plus the exit code and program name.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-29: accepted -> closed. Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
