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
completed: 2026-11-06
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decision: `Curl.Networking` only carries curl's exit 7 text on `DatagramPortUnreachableException`; the exit code itself is set by `Curl.Engine`'s `EndPointReportingProtocolHandler`, already pinned by `EndPointReportingProtocolHandlerTests`. So the networking test now says what it checks: renamed to `ReceiveAsync_PortClosed_ThrowsCouldntConnectTextWithTheElapsedTime` (and the send test to `..._ThrowsCouldntConnectText`), with a `CouldntConnectWords` constant documented as `CurlExitCode.CouldntConnect`'s strerror words and an `Assert.EndsWith` on it. Reproduction now matches. No production code changed, so no quality run was needed.
- `dotnet build` clean; `Curl.Networking.UnitTests` fast tests 4031 passed (the only project changed). Platform-neutral: no paths or OS text.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. Test renamed to what it checks and asserts CouldntConnect's words; exit 7 mapping pinned in EndPointReportingProtocolHandlerTests
