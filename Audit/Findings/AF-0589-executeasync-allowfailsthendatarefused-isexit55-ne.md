---
id: AF-0589
title: ExecuteAsync_AllowFailsThenDataRefused_IsExit55 never checks exit 55
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs:ExecuteAsync_AllowFailsThenDataRefused_IsExit55:name-lies
reproduction: none
task: BL-4753
tasks: BL-4753
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0589 - ExecuteAsync_AllowFailsThenDataRefused_IsExit55 never checks exit 55

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs:313`: ExecuteAsync_AllowFailsThenDataRefused_IsExit55 never checks exit 55. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs:313`

Step 0 name-lies. The test asserts the bytes sent and Assert.AreEqual("DATA failed: 554", run.Result.ErrorMessage); run.Result.ExitCode (SendError, 55) is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs -Pattern 'AllowFailsThenDataRefused_IsExit55' -Context 0,12
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode).
- Actual: Only run.Sent and run.Result.ErrorMessage are asserted.

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode) (55).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
