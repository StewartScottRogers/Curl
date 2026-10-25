---
id: AF-0418
title: SwsHttpRequestFraming.ReachesAuthorizationRules' ParseContentLength(...) < 0 can become <= 0 with no test failing, so a Content-Length: 0 request skips the stand-in's authorization rules
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:ReachesAuthorizationRules-lt:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65:<
task: BL-3467
tasks: BL-3467
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed: 2026-10-24
closed-how: mechanical
closed-by: 2026-10-24_2106.md
---
# AF-0418 - SwsHttpRequestFraming.ReachesAuthorizationRules' ParseContentLength(...) < 0 can become <= 0 with no test failing, so a Content-Length: 0 request skips the stand-in's authorization rules

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65`: SwsHttpRequestFraming.ReachesAuthorizationRules' ParseContentLength(...) < 0 can become <= 0 with no test failing, so a Content-Length: 0 request skips the stand-in's authorization rules.

## Evidence

Location: `Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, '... && ParseContentLength(line[ContentLengthPrefix.Length..]) < 0))' -> '<= 0'. ParseContentLength returns -1 only for unparsable digits. With <= 0, a valid Content-Length: 0 makes ReachesAuthorizationRules return false, so the sws stand-in stops applying its auth rules to empty-body requests. That changes the replies upstream cases get, and no test pins it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65:< -Member ReachesAuthorizationRules -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-24 | 2026-10-24_2106.md | reproduces: no | Ran the -Site command: SwsHttpRequestFraming.cs:65 ReachesAuthorizationRules 'ParseContentLength(...) < 0' -> '<= 0', resolvedLine 65, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
