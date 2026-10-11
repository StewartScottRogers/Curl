---
id: BL-2057
title: Fix AF-0164: CI red 26.2 min after a proxy NTLM POST test from BL-2029 failed off Windows (run 38093700231); with the other spells, ciRedMinutes is 92.12, over 60
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-11
completed:
---
# BL-2057 — Fix AF-0164: CI red 26.2 min after a proxy NTLM POST test from BL-2029 failed off Windows (run 38093700231); with the other spells, ciRedMinutes is 92.12, over 60

## Goal

The defect the audit office reported as AF-0164 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0164 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0164-ci-red-26-2-min-after-a-proxy-ntlm-post-test-from.md`.

Location: `logs/DarkFactory-20261010-161355.log:7`

Location: `logs/DarkFactory-20261010-161355.log:7`

ciRedSpells[2]: start 2026-10-10T23:12:54Z (16:12 local), end 23:39:06Z, 26.2 min, runId 38093700231 on 61e19fdc. The failing test is ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsConte..., which arrived in 832bf1be9 'fix(http): send Content-Length: 0 and no body with an NTLM Type 1 POST or PUT (BL-2029)' at 15:57. BL-2032 was filed at 16:13:44 (2f3af319f) and claimed on L3 at 16:18:08. The fix bbed53ec1 'script the proxy NTLM POST test's Type 1 so it passes on Linux and macOS' landed at 16:21:39, and 7 cancelled runs followed before green on bc5e2a1b at 16:39:06. This spell is under 30 min alone, but it brings the total ciRedMinutes to 92.12, which crosses the 60-minute rule.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; $j = Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json; $j.ciRedMinutes; $j.ciRedSpells | Where-Object runId -eq 38093700231
```

- Expected: ciRedMinutes 60 or less.
- Actual: ciRedMinutes 92.12; minutes 26.2 runId 38093700231

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
