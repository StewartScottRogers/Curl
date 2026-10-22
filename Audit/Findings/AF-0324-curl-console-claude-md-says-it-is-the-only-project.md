---
id: AF-0324
title: Curl.Console/CLAUDE.md says it is the only project without a .UnitLibrary suffix; Curl.Api, Curl.Config, Curl.Mcp and Libcurl also lack it
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Curl.Console/CLAUDE.md:OnlyProjectWithoutUnitLibrarySuffix:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0324 - Curl.Console/CLAUDE.md says it is the only project without a .UnitLibrary suffix; Curl.Api, Curl.Config, Curl.Mcp and Libcurl also lack it

## Summary

Low finding from the truthfulness auditor at `Curl.Console/CLAUDE.md:6`: Curl.Console/CLAUDE.md says it is the only project without a .UnitLibrary suffix; Curl.Api, Curl.Config, Curl.Mcp and Libcurl also lack it. Reported by an auditor flagged unreliable in 2026-10-22_0130.md.

## Evidence

Location: `Curl.Console/CLAUDE.md:6`

Curl.Console/CLAUDE.md:6-7: 'This is the only project without a `.UnitLibrary` suffix, because it is an executable rather than a library.' Curl.Api, Curl.Config (also an executable, curl-config), Curl.Mcp (also an executable) and Libcurl are projects at the root without that suffix. The root CLAUDE.md lists them as exceptions.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console/CLAUDE.md -Pattern 'only project without'; Get-ChildItem -Directory | Where-Object { (Test-Path (Join-Path $_.FullName ($_.Name + '.csproj'))) -and $_.Name -notmatch '\.(UnitLibrary|UnitTests|IntegrationTests)$' } | ForEach-Object Name
```

- Expected: Only Curl.Console is printed, or the document no longer says 'only'.
- Actual: Curl.Console/CLAUDE.md:6 says 'the only project'; the command prints Curl.Api, Curl.Config, Curl.Console, Curl.Mcp, Libcurl.

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
