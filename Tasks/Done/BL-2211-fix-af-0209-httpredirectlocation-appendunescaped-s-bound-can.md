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
completed: 2026-10-14
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix is test-only: `Resolve_OtherLocation_FollowsRfc3986` gains two rows in
  `Curl.Protocol.Http.UnitTests/HttpRedirectLocationTests.cs` pinning both sides of the bound:
  `/~user/page` keeps its tilde, and `/a\u007F` (DEL, 0x7F) is escaped as `%7F`. The production
  code was already right. The test project is the library's own tests, so it is covered by
  the library in `touches`.
- The audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so the
  reproduction was done by hand: with line 245 changed to `>= '~'`, the "a tilde, the last
  byte kept as is" row failed (1 failed, 51 passed in HttpRedirectLocationTests); with the
  line restored, all pass. The quality auditor's re-audit runs the tool itself.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. Redirect tests pin '~' kept and DEL escaped, killing the AF-0209 mutant
