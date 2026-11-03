---
id: BL-4748
title: Fix AF-0584: ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4748 — Fix AF-0584: ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49

## Goal

The defect the audit office reported as AF-0584 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0584 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0584-connecttomap-destinationinaninvalidpartition-fails.md`.

Location: `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:513`

Location: `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:513`

Step 0 name-lies. ConnectDestination carries ParseErrorCode (ConnectToMappings.cs:35) but the test asserts only destination.ParseError and IsMapped.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/NetworkingAdversarialTests.cs -Pattern 'ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text' -Context 0,6
```

- Expected: The body asserts destination.ParseErrorCode is SetoptOptionSyntax (49).
- Actual: Only ParseError and IsMapped are asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added `Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, destination.ParseErrorCode)` to the test. Built, targeted tests green; test-only change.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
