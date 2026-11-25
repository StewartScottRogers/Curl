---
id: BL-5501
title: Fix AF-0859: ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation checks only that the message is not empty
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5501 — Fix AF-0859: ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation checks only that the message is not empty

## Goal

The defect the audit office reported as AF-0859 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0859 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs:105`

Location: `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs:105`

The name promises the violation is named. After Assert.ThrowsExactlyAsync<SignalRTransportException>, the only check of the message is line 105 Assert.IsFalse(string.IsNullOrEmpty(failure.Message)); so any non-empty text passes, including a message for a different failure. Its sibling tests pin exact messages (e.g. line 93 'The server closed the connection without a WebSocket Close frame.').

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs -SimpleMatch -Pattern 'Assert.IsFalse(string.IsNullOrEmpty(failure.Message));'
```

- Expected: No match: the test asserts the masked-frame message exactly.
- Actual: SignalRWebSocketSessionTests.cs:105:        Assert.IsFalse(string.IsNullOrEmpty(failure.Message));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The masked-frame test now pins the decoder's exact message "[WS] masked input frame"; reproduction gives no match. SignalR fast tests: 159 passed. Test-only change, no production code.
- [x] criteria met (build clean, fast tests green for the SignalR project).

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-18: Doing -> Done. Test pins the exact masked-frame message; SignalR fast tests green.
