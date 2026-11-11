---
id: BL-4882
title: Fix AF-0688: SendAsync_PortClosed_ThrowsCurlsExit7Text never asserts exit 7
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4882 — Fix AF-0688: SendAsync_PortClosed_ThrowsCurlsExit7Text never asserts exit 7

## Goal

The defect the audit office reported as AF-0688 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0688 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0688-sendasync-portclosed-throwscurlsexit7text-never-as.md`.

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:45`

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:45`

The name states exit 7. The body asserts only Assert.AreEqual("Failed to connect to localhost:1 after 7 ms: Could not connect to server", refused.Message) and the SocketErrorCode. No assertion of 7 or CurlExitCode.CouldntConnect.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs -Pattern 'CouldntConnect|ExitCode'
```

- Expected: A match: an assertion of CurlExitCode.CouldntConnect (7).
- Actual: No match.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix landed with BL-4881 (AF-0687) in 27009902b, which renamed this test `SendAsync_PortClosed_ThrowsCouldntConnectText` (and its receive twin) so the name no longer claims exit 7. It also pinned curl_easy_strerror's CouldntConnect words through `CouldntConnectWords`, whose comment names CurlExitCode.CouldntConnect (exit 7). This layer only carries the text, and the engine's EndPointReportingProtocolHandlerTests pin the mapping to exit 7, so no code change was needed here.
- The reproduction now matches (lines 15, 16, 17, 26, 43). The 7 PortUnreachableReportingDatagramChannelTests pass, and the build is clean. Nothing changed outside the task file, so the full fast test run was not repeated. The test is platform-neutral (fake channel and fake time), so CI on Linux and macOS is unaffected.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. Fixed by BL-4881's 27009902b: the test no longer claims exit 7 and pins the CouldntConnect words; reproduction now matches
