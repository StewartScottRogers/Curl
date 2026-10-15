---
id: AF-0209
title: HttpRedirectLocation.AppendUnescaped's '> ~' bound can become '>= ~' with no test failing, so a redirect would escape '~'
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-14_1815.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:AppendUnescaped-gt:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245:>
task: BL-2211
tasks: BL-2211
found: 2026-10-14
found-at: c1dedd04f2a8e1534e64b664312c5dacc66bdf4a
scorecard: 2026-10-14_0453.md
duplicate-of:
closed: 2026-10-14
closed-how: mechanical
closed-by: 2026-10-14_1815.md
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

- 2026-10-14 | 2026-10-14_1815.md | reproduces: no | Ran the targeted -Site Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:245:> -Member AppendUnescaped -ExcludeBaselineFailures -TimeoutSeconds 600. The site resolved inside AppendUnescaped to line 241, 'if (character is ' ' or > '~')' -> '>= '~'', outcome killed (4395 ms). The 21 HttpAuthUsing tests that fail unmutated were excluded. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_1815.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
