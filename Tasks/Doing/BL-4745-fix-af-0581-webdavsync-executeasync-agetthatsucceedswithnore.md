---
id: BL-4745
title: Fix AF-0581: WebDavSync ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22 never checks exit 22
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4745 — Fix AF-0581: WebDavSync ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22 never checks exit 22

## Goal

The defect the audit office reported as AF-0581 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0581 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0581-webdavsync-executeasync-agetthatsucceedswithnorepo.md`.

Location: `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs:91`

Location: `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs:91`

Step 0 name-lies. The only assertion is Assert.AreEqual("The server answered GET /dav/a.txt with 0", result.ErrorMessage); result.ExitCode is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs -Pattern 'AGetThatSucceedsWithNoReport_FailsWithExit22' -Context 0,10
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
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
