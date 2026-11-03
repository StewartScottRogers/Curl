---
id: BL-4751
title: Fix AF-0587: TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0 never checks exit 23
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4751 — Fix AF-0587: TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0 never checks exit 23

## Goal

The defect the audit office reported as AF-0587 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0587 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0587-transferasync-outputfailswithanotherioexception-ex.md`.

Location: `Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs:238`

Location: `Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs:238`

Step 0 name-lies. The only assertion is Assert.AreEqual("Failure writing output to destination, passed 11 returned 0", result.ErrorMessage); result.ExitCode (WriteError, 23) is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs -Pattern 'Exits23Returned0' -Context 0,10
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added `Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode)` to the test; Smb unit tests pass (199 passed, 10 skipped off-Windows). Test-only change, no production code.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Exit code assertion added
