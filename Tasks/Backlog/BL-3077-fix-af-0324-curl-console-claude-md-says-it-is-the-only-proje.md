---
id: BL-3077
title: Fix AF-0324: Curl.Console/CLAUDE.md says it is the only project without a .UnitLibrary suffix; Curl.Api, Curl.Config, Curl.Mcp and Libcurl also lack it
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-22
completed:
---
# BL-3077 — Fix AF-0324: Curl.Console/CLAUDE.md says it is the only project without a .UnitLibrary suffix; Curl.Api, Curl.Config, Curl.Mcp and Libcurl also lack it

## Goal

The defect the audit office reported as AF-0324 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0324 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0324-curl-console-claude-md-says-it-is-the-only-project.md`.

Location: `Curl.Console/CLAUDE.md:6`

Location: `Curl.Console/CLAUDE.md:6`

Curl.Console/CLAUDE.md:6-7: 'This is the only project without a `.UnitLibrary` suffix, because it is an executable rather than a library.' Curl.Api, Curl.Config (also an executable, curl-config), Curl.Mcp (also an executable) and Libcurl are projects at the root without that suffix. The root CLAUDE.md lists them as exceptions.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console/CLAUDE.md -Pattern 'only project without'; Get-ChildItem -Directory | Where-Object { (Test-Path (Join-Path $_.FullName ($_.Name + '.csproj'))) -and $_.Name -notmatch '\.(UnitLibrary|UnitTests|IntegrationTests)$' } | ForEach-Object Name
```

- Expected: Only Curl.Console is printed, or the document no longer says 'only'.
- Actual: Curl.Console/CLAUDE.md:6 says 'the only project'; the command prints Curl.Api, Curl.Config, Curl.Console, Curl.Mcp, Libcurl.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
