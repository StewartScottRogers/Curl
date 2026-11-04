---
id: BL-4845
title: Fix AF-0663: Authentication CLAUDE.md says CurlComposition sets NtlmHttpAuthenticator's matchesSspiBuild; EngineComposition does
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4845 — Fix AF-0663: Authentication CLAUDE.md says CurlComposition sets NtlmHttpAuthenticator's matchesSspiBuild; EngineComposition does

## Goal

The defect the audit office reported as AF-0663 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0663 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0663-authentication-claude-md-says-curlcomposition-sets.md`.

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:38`

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:38`

CLAUDE.md lines 37-38: 'where the SSPI build is matched (the constructor's `matchesSspiBuild`, set on Windows by `CurlComposition`)'. The only production construction is Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs:42: 'new NtlmHttpAuthenticator(securityContexts, matchesSspiBuild: OperatingSystem.IsWindows(), diagnosticLog)'. Curl.Console/CurlComposition.cs never constructs NtlmHttpAuthenticator; it reaches it only through EngineComposition (using static Curl.Engine.EngineComposition, line 45). The same paragraph's later SASL sentence already names EngineComposition, so the two disagree and send a reader to the wrong type.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitLibrary/CLAUDE.md -Pattern 'by .CurlComposition.'; Select-String -Path Curl.Console/CurlComposition.cs,Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs -Pattern 'new NtlmHttpAuthenticator'
```

- Expected: The CLAUDE.md names the type that passes matchesSspiBuild: no match for 'by `CurlComposition`', and the construction it names is the one found.
- Actual: CLAUDE.md:38: Windows by `CurlComposition`) unless the context is ...; the only construction found is EngineComposition.Authentication.cs:42: new NtlmHttpAuthenticator(securityContexts, matchesSspiBuild: OperatingSystem.IsWindows(), diagnosticLog)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

CLAUDE.md line 38 now names `EngineComposition`. Docs only; no code changed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. CLAUDE.md now names EngineComposition; reproduction no longer matches. Docs-only change.
