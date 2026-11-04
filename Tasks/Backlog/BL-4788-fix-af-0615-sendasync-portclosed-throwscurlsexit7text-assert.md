---
id: BL-4788
title: Fix AF-0615: SendAsync_PortClosed_ThrowsCurlsExit7Text asserts only the text its own fake supplies, never exit 7 or curl's exit-7 text
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4788 — Fix AF-0615: SendAsync_PortClosed_ThrowsCurlsExit7Text asserts only the text its own fake supplies, never exit 7 or curl's exit-7 text

## Goal

The defect the audit office reported as AF-0615 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0615 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0615-sendasync-portclosed-throwscurlsexit7text-asserts.md`.

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:43`

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:43`

The test builds new PortUnreachableReportingDatagramChannel(new FailingChannel(...), () => "refused text") and its only assertion is Assert.AreEqual("refused text", refused.Message): the value its own fake returns. Neither 7, CurlExitCode.CouldntConnect nor curl's 'Failed to connect ... Could not connect to server' text appears, so the test cannot fail if a send to a closed port reports anything else. Its sibling ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime does assert curl's text.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs -Pattern 'SendAsync_PortClosed_ThrowsCurlsExit7Text' -Context 0,14
```

- Expected: The body asserts curl's exit-7 text (or exit 7), not only the fake's own string.
- Actual: Only Assert.AreEqual("refused text", refused.Message); with () => "refused text" set up in the same test.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
