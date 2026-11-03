---
id: BL-4752
title: Fix AF-0588: ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8 never checks exit 8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4752 — Fix AF-0588: ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8 never checks exit 8

## Goal

The defect the audit office reported as AF-0588 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0588 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0588-executeasync-fileundernobody-reportsthedatathenshu.md`.

Location: `Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs:110`

Location: `Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs:110`

Step 0 name-lies. The test keeps only the transcript and asserts CollectionAssert.AreEqual(new[] { "<= hello world", "* shutting down connection #0" }, transcript); the transfer's exit code 8 is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs -Pattern 'NoLineForExit8' -Context 0,6
```

- Expected: The body asserts the result's ExitCode is WeirdServerReply (8).
- Actual: Only the transcript is asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
