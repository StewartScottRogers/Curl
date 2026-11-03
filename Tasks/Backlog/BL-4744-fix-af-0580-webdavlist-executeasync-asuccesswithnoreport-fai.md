---
id: BL-4744
title: Fix AF-0580: WebDavList ExecuteAsync_ASuccessWithNoReport_FailsWithExit22 never checks exit 22
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4744 — Fix AF-0580: WebDavList ExecuteAsync_ASuccessWithNoReport_FailsWithExit22 never checks exit 22

## Goal

The defect the audit office reported as AF-0580 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0580 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0580-webdavlist-executeasync-asuccesswithnoreport-fails.md`.

Location: `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs:62`

Location: `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs:62`

Step 0 name-lies. The only assertion is Assert.AreEqual("The server answered PROPFIND with 0, not 207 Multistatus", result.ErrorMessage); result.ExitCode (HttpReturnedError, 22) is not checked, though line 56 of the same file shows the check.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs -Pattern 'ASuccessWithNoReport_FailsWithExit22' -Context 0,5
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
