---
id: BL-5786
title: Fix AF-0984: ReceiveAsync_MaskedServerFrame_FailsWithTheViolation checks only that the message is non-empty, not that it names the violation
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5786 — Fix AF-0984: ReceiveAsync_MaskedServerFrame_FailsWithTheViolation checks only that the message is non-empty, not that it names the violation

## Goal

The defect the audit office reported as AF-0984 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0984 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs:124`

Location: `Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs:124`

Step 1 (whole project read). After Assert.ThrowsExactlyAsync<Http2WebSocketException>(...), the only check of the failure is Assert.IsFalse(string.IsNullOrEmpty(failure.Message)). The name promises a failure 'with the violation' (RFC 6455 5.1: a server never masks its frames), but any non-empty message, including one about some other error, passes. Every sibling test in the file pins its text with StringAssert.Contains.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs -Pattern 'IsNullOrEmpty\(failure.Message\)'
```

- Expected: No match: the test pins the masked-frame message, for example with StringAssert.Contains.
- Actual: Http2WebSocketTests.cs:124:        Assert.IsFalse(string.IsNullOrEmpty(failure.Message));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now pins "masked input frame" (the WsFrameDecoder violation text) with StringAssert.Contains; platform-neutral.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-22: Doing -> Done. Test pins the masked input frame violation text.
