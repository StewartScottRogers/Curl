---
id: BL-5073
title: Fix AF-0714: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5073 — Fix AF-0714: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup

## Goal

The defect the audit office reported as AF-0714 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0714 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0714-surviving-mutant-a-handle-s-cookie-jar-can-be-drop.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78`

Mutant 'cookies is not null && IsHttpUrl(...)' -> 'cookies is not null || IsHttpUrl(...)' survived. With it, an HTTP transfer with no cookie engine sets handleCookieJar to null (the jar written at cleanup loses its cookies), and an FTP transfer with cookies replaces the jar. Both change the bytes of the CURLOPT_COOKIEJAR file.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78:&& -Member CleanUpUnlessToolDoesAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
