---
id: AF-0365
title: ManagedCurlUrlHandle.Login's || can become && with no test failing, so a URL with only a password loses its login when read back
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-23_0618.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:Login-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225:||
task: BL-3218
tasks: BL-3218
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed: 2026-10-23
closed-how: mechanical
closed-by: 2026-10-23_0618.md
---
# AF-0365 - ManagedCurlUrlHandle.Login's || can become && with no test failing, so a URL with only a password loses its login when read back

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225`: ManagedCurlUrlHandle.Login's || can become && with no test failing, so a URL with only a password loses its login when read back. Reported by an auditor flagged unreliable in 2026-10-22_1902.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original: bool any = parts.User is not null || parts.Password is not null || options is not null; mutated: parts.User is not null || parts.Password is not null && options is not null. With a password and no user or options (http://:pw@host/), `any` becomes false and curl_url_get(CURLUPART_URL) drops the `:pw@` login from the URL text it returns. The URL API's output changes and ManagedCurlUrlHandleTests do not notice.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225:|| -Member Login -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-23 | 2026-10-23_0618.md | reproduces: no | Ran the -Site command: site ManagedCurlUrlHandle.cs:225:|| in Login resolved to line 229 (parts.User is not null || parts.Password is not null || options is not null). Outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-23: accepted -> closed. Re-audit 2026-10-23_0618.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
