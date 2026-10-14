---
id: AF-0209
title: HttpRedirectLocation.AppendUnescaped's '> ~' bound can become '>= ~' with no test failing, so a redirect would escape '~'
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:AppendUnescaped-gt:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245:>
task: BL-2211
tasks: BL-2211
found: 2026-10-14
found-at: c1dedd04f2a8e1534e64b664312c5dacc66bdf4a
scorecard: 2026-10-14_0453.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0209 - HttpRedirectLocation.AppendUnescaped's '> ~' bound can become '>= ~' with no test failing, so a redirect would escape '~'

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245`: HttpRedirectLocation.AppendUnescaped's '> ~' bound can become '>= ~' with no test failing, so a redirect would escape '~'.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0 left this mutant surviving: 'if (character is ' ' or > '~')' -> 'if (character is ' ' or >= '~')'. The doc comment says only a space or a byte above 0x7E is escaped. The mutant also percent-escapes '~' (0x7E), so following a Location such as /~user/page would request /%7Euser/page: different request bytes from curl, and a different resource on many servers. No test in HttpRedirectLocationTests uses '~' (Select-String for '~' finds none), so the bound is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245:> -Member AppendUnescaped -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test resolves a Location containing '~' and expects it kept as is.
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
