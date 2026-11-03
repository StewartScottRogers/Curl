---
id: BL-4737
title: Fix AF-0573: ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56 never checks exit 56
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ContentDigest.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4737 — Fix AF-0573: ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56 never checks exit 56

## Goal

The defect the audit office reported as AF-0573 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0573 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0573-executeasync-areprdigestsplitovertwofieldlinesthat.md`.

Location: `Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs:69`

Location: `Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs:69`

Step 0 name-lies. The only assertion is Assert.AreEqual("Repr-Digest sha-512 does not match the response body", result.ErrorMessage); result.ExitCode (CurlExitCode.RecvError, 56) is never checked, though its sibling test at line 62 checks it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs -Pattern 'SplitOverTwoFieldLinesThatDoesNotMatch' -Context 0,5
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode).
- Actual: The body asserts only result.ErrorMessage.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
