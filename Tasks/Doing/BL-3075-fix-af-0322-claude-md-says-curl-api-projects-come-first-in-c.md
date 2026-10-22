---
id: BL-3075
title: Fix AF-0322: CLAUDE.md says Curl.Api projects come first in Curl.slnx and Libcurl sorts after every Curl.* project; the slnx groups by folder and does neither
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3075 — Fix AF-0322: CLAUDE.md says Curl.Api projects come first in Curl.slnx and Libcurl sorts after every Curl.* project; the slnx groups by folder and does neither

## Goal

The defect the audit office reported as AF-0322 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0322 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0322-claude-md-says-curl-api-projects-come-first-in-cur.md`.

Location: `CLAUDE.md:322`

Location: `CLAUDE.md:322`

CLAUDE.md:319-322: '`Libcurl` and `Libcurl.UnitTests` sort after every `Curl.*` project; in `Curl.slnx`, `Curl.Api`, `Curl.Api.UnitLibrary` and `Curl.Api.UnitTests` come first, before `Curl.Authentication.UnitLibrary`.' Curl.slnx lists the /Conformance/ and /Protocols/ folders first (lines 88-179). Curl.Api appears at line 186 under /Hosts/Api/, and Libcurl at line 196, before Curl.Mcp, Curl.Cli, Curl.Core and the other Curl.* projects (ADR-0731 grouping). Keep it that way is stated as a rule, so an agent may reorder the solution file against ADR-0731.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern 'come first, before'; $p = Select-String -Path Curl.slnx -Pattern 'Project Path="([^"]+)\.csproj' | ForEach-Object { $_.Matches[0].Groups[1].Value }; 'first: ' + $p[0]; 'Curl.Api index: ' + [array]::IndexOf($p, 'Curl.Api/Curl.Api'); 'Libcurl index: ' + [array]::IndexOf($p, 'Libcurl/Libcurl') + ' of ' + $p.Count
```

- Expected: first: Curl.Api/Curl.Api, and the Libcurl index after every Curl.* entry.
- Actual: first: Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary; Curl.Api comes after the Conformance and Protocols projects; Libcurl sits mid-list, before Curl.Mcp, Curl.Cli and the rest.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
