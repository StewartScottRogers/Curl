---
id: AF-0859
title: ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation checks only that the message is not empty
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs:ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation_Rfc6455Section5Point1:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0859 - ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation checks only that the message is not empty

## Summary

Low finding from the quality auditor at `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs:105`: ReceiveRecordAsync_MaskedServerFrame_ThrowsTheViolation checks only that the message is not empty. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs:105`

The name promises the violation is named. After Assert.ThrowsExactlyAsync<SignalRTransportException>, the only check of the message is line 105 Assert.IsFalse(string.IsNullOrEmpty(failure.Message)); so any non-empty text passes, including a message for a different failure. Its sibling tests pin exact messages (e.g. line 93 'The server closed the connection without a WebSocket Close frame.').

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.SignalR.UnitTests/Transport/SignalRWebSocketSessionTests.cs -SimpleMatch -Pattern 'Assert.IsFalse(string.IsNullOrEmpty(failure.Message));'
```

- Expected: No match: the test asserts the masked-frame message exactly.
- Actual: SignalRWebSocketSessionTests.cs:105:        Assert.IsFalse(string.IsNullOrEmpty(failure.Message));

## Re-audits

## Log

- 2026-11-18: filed proposed.
