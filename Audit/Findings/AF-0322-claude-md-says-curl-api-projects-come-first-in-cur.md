---
id: AF-0322
title: CLAUDE.md says Curl.Api projects come first in Curl.slnx and Libcurl sorts after every Curl.* project; the slnx groups by folder and does neither
auditor: truthfulness
severity: Medium
status: accepted
reason:
key: truthfulness:CLAUDE.md:SlnxApiFirstOrder:false-statement
reproduction: none
task: BL-3075
tasks: BL-3075
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0322 - CLAUDE.md says Curl.Api projects come first in Curl.slnx and Libcurl sorts after every Curl.* project; the slnx groups by folder and does neither

## Summary

Medium finding from the truthfulness auditor at `CLAUDE.md:322`: CLAUDE.md says Curl.Api projects come first in Curl.slnx and Libcurl sorts after every Curl.* project; the slnx groups by folder and does neither. Reported by an auditor flagged unreliable in 2026-10-22_0130.md.

## Evidence

Location: `CLAUDE.md:322`

CLAUDE.md:319-322: '`Libcurl` and `Libcurl.UnitTests` sort after every `Curl.*` project; in `Curl.slnx`, `Curl.Api`, `Curl.Api.UnitLibrary` and `Curl.Api.UnitTests` come first, before `Curl.Authentication.UnitLibrary`.' Curl.slnx lists the /Conformance/ and /Protocols/ folders first (lines 88-179). Curl.Api appears at line 186 under /Hosts/Api/, and Libcurl at line 196, before Curl.Mcp, Curl.Cli, Curl.Core and the other Curl.* projects (ADR-0731 grouping). Keep it that way is stated as a rule, so an agent may reorder the solution file against ADR-0731.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern 'come first, before'; $p = Select-String -Path Curl.slnx -Pattern 'Project Path="([^"]+)\.csproj' | ForEach-Object { $_.Matches[0].Groups[1].Value }; 'first: ' + $p[0]; 'Curl.Api index: ' + [array]::IndexOf($p, 'Curl.Api/Curl.Api'); 'Libcurl index: ' + [array]::IndexOf($p, 'Libcurl/Libcurl') + ' of ' + $p.Count
```

- Expected: first: Curl.Api/Curl.Api, and the Libcurl index after every Curl.* entry.
- Actual: first: Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary; Curl.Api comes after the Conformance and Protocols projects; Libcurl sits mid-list, before Curl.Mcp, Curl.Cli and the rest.

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
