---
id: BL-3523
title: Fix AF-0446: Lib555's RunAsync_SetsTheIoctlCallbackAndPerforms never checks that the ioctl callback is set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3523 — Fix AF-0446: Lib555's RunAsync_SetsTheIoctlCallbackAndPerforms never checks that the ioctl callback is set

## Goal

The defect the audit office reported as AF-0446 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0446 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0446-lib555-s-runasync-setstheioctlcallbackandperforms.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:111`

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:111`

The body runs Lib555 against a FakeCurlEngine with no Performing hook. It asserts only Assert.AreEqual((int)CurlCode.Ok, result.ExitCode) and the ProgramName "lib555". It never reads options.Callbacks for the Ioctlfunction/Ioctldata that Curl.Conformance.LibTest.UnitLibrary/Lib555.cs:51-52 sets, and no other Lib555 test does either (only this one and a null-argument test exist). Removing the two Ioctl option lines from Lib555 would leave the test green. The twin test for Lib553 (Lib539To553Tests.cs:377) does call the reader's Ioctl.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs -Pattern 'RunAsync_SetsTheIoctlCallbackAndPerforms' -Context 0,8
```

- Expected: The body captures the performed EasyOptions and asserts that its Ioctl callback is the reader's Ioctl (for example that it answers IoctlResult.Ok to RestartRead).
- Actual: The body asserts only exit code 0 and ProgramName "lib555"; it never mentions Ioctl.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. Lib555 test now asserts the ioctl callback is set and answers Ok to RestartRead
