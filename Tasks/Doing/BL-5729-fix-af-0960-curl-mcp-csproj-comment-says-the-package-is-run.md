---
id: BL-5729
title: Fix AF-0960: Curl.Mcp.csproj comment says the package is run with 'dnx Curl.Mcp'; its PackageId is Surly.Curl.Mcp
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Mcp/Curl.Mcp.csproj]
requirement: none
created: 2026-11-21
completed:
---
# BL-5729 — Fix AF-0960: Curl.Mcp.csproj comment says the package is run with 'dnx Curl.Mcp'; its PackageId is Surly.Curl.Mcp

## Goal

The defect the audit office reported as AF-0960 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0960 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Mcp/Curl.Mcp.csproj:11`

Location: `Curl.Mcp/Curl.Mcp.csproj:11`

Lines 10-11: "a .NET tool package of PackageType McpServer that `dnx Curl.Mcp` runs". The same file's line 29 sets <PackageId>Surly.Curl.Mcp</PackageId> (ADR-1333). The comment was not updated with the rename.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Mcp/Curl.Mcp.csproj -Pattern 'dnx Curl\.Mcp|<PackageId>'
```

- Expected: The comment names the package ID it sets: dnx Surly.Curl.Mcp.
- Actual: 11: ...that `dnx Curl.Mcp` runs...; 29: <PackageId>Surly.Curl.Mcp</PackageId>

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
