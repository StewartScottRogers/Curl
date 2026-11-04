---
id: AF-0663
title: Authentication CLAUDE.md says CurlComposition sets NtlmHttpAuthenticator's matchesSspiBuild; EngineComposition does
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Curl.Authentication.UnitLibrary/CLAUDE.md:NtlmHttpAuthenticator.matchesSspiBuild:false-statement
reproduction: none
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0663 - Authentication CLAUDE.md says CurlComposition sets NtlmHttpAuthenticator's matchesSspiBuild; EngineComposition does

## Summary

Low finding from the truthfulness auditor at `Curl.Authentication.UnitLibrary/CLAUDE.md:38`: Authentication CLAUDE.md says CurlComposition sets NtlmHttpAuthenticator's matchesSspiBuild; EngineComposition does. Reported by an auditor flagged unreliable in 2026-11-04_0643.md.

## Evidence

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:38`

CLAUDE.md lines 37-38: 'where the SSPI build is matched (the constructor's `matchesSspiBuild`, set on Windows by `CurlComposition`)'. The only production construction is Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs:42: 'new NtlmHttpAuthenticator(securityContexts, matchesSspiBuild: OperatingSystem.IsWindows(), diagnosticLog)'. Curl.Console/CurlComposition.cs never constructs NtlmHttpAuthenticator; it reaches it only through EngineComposition (using static Curl.Engine.EngineComposition, line 45). The same paragraph's later SASL sentence already names EngineComposition, so the two disagree and send a reader to the wrong type.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitLibrary/CLAUDE.md -Pattern 'by .CurlComposition.'; Select-String -Path Curl.Console/CurlComposition.cs,Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs -Pattern 'new NtlmHttpAuthenticator'
```

- Expected: The CLAUDE.md names the type that passes matchesSspiBuild: no match for 'by `CurlComposition`', and the construction it names is the one found.
- Actual: CLAUDE.md:38: Windows by `CurlComposition`) unless the context is ...; the only construction found is EngineComposition.Authentication.cs:42: new NtlmHttpAuthenticator(securityContexts, matchesSspiBuild: OperatingSystem.IsWindows(), diagnosticLog)

## Re-audits

## Log

- 2026-11-04: filed proposed.
