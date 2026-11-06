---
id: BL-4881
title: Fix AF-0687: ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime never asserts exit 7
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-4881 — Fix AF-0687: ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime never asserts exit 7

## Goal

The defect the audit office reported as AF-0687 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0687 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0687-receiveasync-portclosed-throwscurlsexit7textwithth.md`.

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:22`

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:22`

The name states exit 7. The body asserts only the exception type, Assert.AreEqual("Failed to connect to localhost:1 after 5 ms: Could not connect to server", refused.Message), and the SocketErrorCode. Nothing checks 7 or CurlExitCode.CouldntConnect, so the exception could map to any exit code. Find-WeakTests flagged it as name-lies.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs -Pattern 'CouldntConnect|ExitCode'
```

- Expected: A match: an assertion of CurlExitCode.CouldntConnect (7).
- Actual: No match.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
