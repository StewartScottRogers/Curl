---
id: AF-0770
title: Surviving mutant: curl_ws_start_frame for a zero-length upload frame can answer SendError
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:StartUploadFrame-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:229:<
task: BL-5219
tasks: BL-5219
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_0222.md
---
# AF-0770 - Surviving mutant: curl_ws_start_frame for a zero-length upload frame can answer SendError

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:229`: Surviving mutant: curl_ws_start_frame for a zero-length upload frame can answer SendError. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:229`

Mutant 'if (frameLength < 0 || !UploadFrames.CanStartFrame)' -> 'frameLength <= 0' survived. The doc comment says SendError is for a negative length only; a zero-length frame (an empty text frame or a close) is valid. The mutant changes the CurlCode the libcurl API returns for it, and no Engine test starts a zero-length upload frame.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs:229:< -Member StartUploadFrame -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the -Site command: resolvedLine 229, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
