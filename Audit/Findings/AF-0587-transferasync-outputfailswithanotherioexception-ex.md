---
id: AF-0587
title: TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0 never checks exit 23
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs:TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0:name-lies
reproduction: none
task: BL-4751
tasks: BL-4751
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0587 - TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0 never checks exit 23

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs:238`: TransferAsync_OutputFailsWithAnotherIOException_Exits23Returned0 never checks exit 23. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs:238`

Step 0 name-lies. The only assertion is Assert.AreEqual("Failure writing output to destination, passed 11 returned 0", result.ErrorMessage); result.ExitCode (WriteError, 23) is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smb.UnitTests/SmbFileTransferTests.cs -Pattern 'Exits23Returned0' -Context 0,10
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
