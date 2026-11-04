---
id: AF-0628
title: Surviving mutant: a resumable-upload header value that starts with CR or LF can escape the injection refusal, and no test fails
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:FormatHead-ge:surviving-mutant
reproduction: mutation Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51:>=
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
# AF-0628 - Surviving mutant: a resumable-upload header value that starts with CR or LF can escape the injection refusal, and no test fails

## Summary

High finding from the quality auditor at `Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51`: Surviving mutant: a resumable-upload header value that starts with CR or LF can escape the injection refusal, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51`

Invoke-MutationTest -Library Curl.Extension.ResumableUpload.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'value.AsSpan().IndexOfAny('\r', '\n') >= 0' -> '> 0'. A header value whose first character is CR or LF ('\r\nX-Injected: 1') is then written into the request head and splits it. SendAsync_FieldHoldsCrOrLf_ThrowsArgumentException has only the DataRows ('X-Bad\r\nInjected', 'v') and ('X-Bad', 'v\nInjected: 1'), so the CR or LF is never at index 0 of the value.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51:>= -Member FormatHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
