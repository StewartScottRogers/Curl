---
id: BL-4790
title: Fix AF-0617: Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4790 — Fix AF-0617: Surviving mutant: NTLM's HeaderOf can send a header for an empty token or a refused step without a test failing

## Goal

The defect the audit office reported as AF-0617 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0617 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0617-surviving-mutant-ntlm-s-headerof-can-send-a-header.md`.

Location: `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183`

Location: `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183`

Mutant 'step.Status is ContinueNeeded or Completed && step.Token.Length != 0' -> '... || step.Token.Length != 0' survived. With it, a ContinueNeeded/Completed step with an empty token sends 'NTLM ' with an empty value, and a Refused/MalformedToken step with a token sends that token instead of nothing. Either way the Authorization request bytes change; no test pins an empty-token step.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs:183:&& -Member HeaderOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
