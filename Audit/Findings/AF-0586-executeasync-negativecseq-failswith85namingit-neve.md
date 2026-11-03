---
id: AF-0586
title: ExecuteAsync_NegativeCSeq_FailsWith85NamingIt never checks exit 85
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs:ExecuteAsync_NegativeCSeq_FailsWith85NamingIt:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0586 - ExecuteAsync_NegativeCSeq_FailsWith85NamingIt never checks exit 85

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs:761`: ExecuteAsync_NegativeCSeq_FailsWith85NamingIt never checks exit 85. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs:761`

Step 0 name-lies. The only assertion is Assert.AreEqual("The CSeq of this request 1 did not match the response -3", result.ErrorMessage); result.ExitCode (RtspCseqError, 85) is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs -Pattern 'NegativeCSeq_FailsWith85NamingIt' -Context 0,7
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
