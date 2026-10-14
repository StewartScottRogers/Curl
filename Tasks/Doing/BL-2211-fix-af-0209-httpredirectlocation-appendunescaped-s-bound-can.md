---
id: BL-2211
title: Fix AF-0209: HttpRedirectLocation.AppendUnescaped's '> ~' bound can become '>= ~' with no test failing, so a redirect would escape '~'
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2211 — Fix AF-0209: HttpRedirectLocation.AppendUnescaped's '> ~' bound can become '>= ~' with no test failing, so a redirect would escape '~'

## Goal

The defect the audit office reported as AF-0209 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0209 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0209-httpredirectlocation-appendunescaped-s-bound-can-b.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245`

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0 left this mutant surviving: 'if (character is ' ' or > '~')' -> 'if (character is ' ' or >= '~')'. The doc comment says only a space or a byte above 0x7E is escaped. The mutant also percent-escapes '~' (0x7E), so following a Location such as /~user/page would request /%7Euser/page: different request bytes from curl, and a different resource on many servers. No test in HttpRedirectLocationTests uses '~' (Select-String for '~' finds none), so the bound is unpinned.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245:> -Member AppendUnescaped -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test resolves a Location containing '~' and expects it kept as is.
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
