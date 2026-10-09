---
id: BL-1863
title: Fix AF-0113: SendBodyAsync's no-body 'return false' can become 'return true' with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1863 — Fix AF-0113: SendBodyAsync's no-body 'return false' can become 'return true' with no test failing

## Goal

The defect the audit office reported as AF-0113 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0113 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0113-sendbodyasync-s-no-body-return-false-can-become-re.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433`

Mutant 'return false;' -> 'return true;' at line 2433 survived (sampled run, seed 0). The return value is bodyLeftUnsent (line 1129): true means a final status arrived during the 100-continue wait and the body was left unsent. For a request with no body, the mutant makes ReportRequestSent skip nothing visible but makes RetryOfAsync/RetriesWithoutExpect (line 2148) treat a keep-alive 417 as 'body stopped': it reports 'Got 417 while waiting for a 100' and retries with plan.Framing.Body! being null. A bodyless request answered by 417 would then be resent or fail instead of ending with the 417, changing request bytes and exit code. No test sends a bodyless request that gets a 417.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433:false -Member SendBodyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
