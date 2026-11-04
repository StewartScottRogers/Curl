---
id: AF-0573
title: ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56 never checks exit 56
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs:ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56_Rfc9530Section3:name-lies
reproduction: none
task: BL-4737
tasks: BL-4737
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0573 - ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56 never checks exit 56

## Summary

High finding from the quality auditor at `Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs:69`: ExecuteAsync_AReprDigestSplitOverTwoFieldLinesThatDoesNotMatch_FailsWithExit56 never checks exit 56. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs:69`

Step 0 name-lies. The only assertion is Assert.AreEqual("Repr-Digest sha-512 does not match the response body", result.ErrorMessage); result.ExitCode (CurlExitCode.RecvError, 56) is never checked, though its sibling test at line 62 checks it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.ContentDigest.UnitTests/ContentDigestTransferHookTests.cs -Pattern 'SplitOverTwoFieldLinesThatDoesNotMatch' -Context 0,5
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode).
- Actual: The body asserts only result.ErrorMessage.

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode) (56) and the message.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
