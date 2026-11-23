---
id: AF-0984
title: ReceiveAsync_MaskedServerFrame_FailsWithTheViolation checks only that the message is non-empty, not that it names the violation
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs:ReceiveAsync_MaskedServerFrame_FailsWithTheViolation:weak-assertion
reproduction: none
task: BL-5786
tasks: BL-5786
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0984 - ReceiveAsync_MaskedServerFrame_FailsWithTheViolation checks only that the message is non-empty, not that it names the violation

## Summary

Low finding from the quality auditor at `Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs:124`: ReceiveAsync_MaskedServerFrame_FailsWithTheViolation checks only that the message is non-empty, not that it names the violation.

## Evidence

Location: `Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs:124`

Step 1 (whole project read). After Assert.ThrowsExactlyAsync<Http2WebSocketException>(...), the only check of the failure is Assert.IsFalse(string.IsNullOrEmpty(failure.Message)). The name promises a failure 'with the violation' (RFC 6455 5.1: a server never masks its frames), but any non-empty message, including one about some other error, passes. Every sibling test in the file pins its text with StringAssert.Contains.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2/Http2WebSocketTests.cs -Pattern 'IsNullOrEmpty\(failure.Message\)'
```

- Expected: No match: the test pins the masked-frame message, for example with StringAssert.Contains.
- Actual: Http2WebSocketTests.cs:124:        Assert.IsFalse(string.IsNullOrEmpty(failure.Message));

## Re-audits

## Log

- 2026-11-22: filed proposed.
