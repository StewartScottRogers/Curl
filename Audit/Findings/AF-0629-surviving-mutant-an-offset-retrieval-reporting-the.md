---
id: AF-0629
title: Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:OffsetIn-le:surviving-mutant
reproduction: mutation Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47:<=
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0629 - Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails

## Summary

High finding from the quality auditor at `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47`: Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47`

Invoke-MutationTest -Library Curl.Extension.ResumableUpload.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'offset <= length' -> 'offset < length' in OffsetIn. When the server's HEAD says every byte arrived (Upload-Offset equal to the body length), the mutant throws ResumableUploadTransferException(CurlExitCode.WeirdServerReply, ...) instead of sending the final PATCH, so a resumed upload ends with exit 8. The tests cover offset 3 of 5 and a missing offset, never offset == length.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47:<= -Member OffsetIn -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
