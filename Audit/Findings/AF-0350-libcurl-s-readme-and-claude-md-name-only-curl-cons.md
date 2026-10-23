---
id: AF-0350
title: Libcurl's README and CLAUDE.md name only Curl.Console and Curl.Api beside it as naming exceptions; Curl.Config and Curl.Mcp are too
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-22_1902.md: the reproduction no longer reproduces.
key: truthfulness:Libcurl/README.md:Libcurl:false-statement
reproduction: none
task: BL-3130
tasks: BL-3130
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed: 2026-10-22
closed-how: reliable-reaudit
closed-by: 2026-10-22_1902.md
---
# AF-0350 - Libcurl's README and CLAUDE.md name only Curl.Console and Curl.Api beside it as naming exceptions; Curl.Config and Curl.Mcp are too

## Summary

Low finding from the truthfulness auditor at `Libcurl/README.md:5`: Libcurl's README and CLAUDE.md name only Curl.Console and Curl.Api beside it as naming exceptions; Curl.Config and Curl.Mcp are too.

## Evidence

Location: `Libcurl/README.md:5`

Libcurl/README.md line 5 says: 'with `Curl.Console` and `Curl.Api` it is a naming exception', and Libcurl/CLAUDE.md line 4 says: 'A naming exception, with `Curl.Console` and `Curl.Api`.' The root CLAUDE.md says five projects are exceptions to the Curl.<Area>.UnitLibrary rule. The repository root holds five C# projects without a .UnitLibrary/.UnitTests/.IntegrationTests suffix: Curl.Api, Curl.Config, Curl.Console, Curl.Mcp and Libcurl. The two documents disagree with the root CLAUDE.md and leave out Curl.Config and Curl.Mcp.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Libcurl/README.md, Libcurl/CLAUDE.md -Pattern 'naming exception' | ForEach-Object { '{0}:{1}: {2}' -f $_.Filename, $_.LineNumber, $_.Line.Trim() }; Get-ChildItem -Directory | Where-Object { (Test-Path (Join-Path $_.FullName ($_.Name + '.csproj'))) -and $_.Name -notmatch '\.(UnitLibrary|UnitTests|IntegrationTests)$' } | ForEach-Object Name
```

- Expected: The exceptions the Libcurl documents name match the five projects listed (or the documents point to the root CLAUDE.md's list).
- Actual: README.md:5: `Curl.Console` and `Curl.Api` it is a naming exception. ... / CLAUDE.md:4: ... A naming exception, with `Curl.Console` and `Curl.Api`. / Curl.Api Curl.Config Curl.Console Curl.Mcp Libcurl

## Re-audits

- 2026-10-22 | 2026-10-22_1902.md | reproduces: no | Ran the reproduction: README.md:7 names `Curl.Api`, `Curl.Config`, `Curl.Console` and `Curl.Mcp`, and CLAUDE.md:4 says 'A naming exception, with `Curl.Api`, `Curl.Config`, `Curl.Console` and `Curl.Mcp`.' The directory scan lists Curl.Api, Curl.Config, Curl.Console, Curl.Mcp and Libcurl, so the docs now name every other exception.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_1902.md: the reproduction no longer reproduces.
