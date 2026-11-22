---
id: BL-5728
title: Fix AF-0959: README.md tells readers to run the MCP server with 'dnx Curl.Mcp', but the package ID is Surly.Curl.Mcp
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md]
requirement: none
created: 2026-11-21
completed:
---
# BL-5728 — Fix AF-0959: README.md tells readers to run the MCP server with 'dnx Curl.Mcp', but the package ID is Surly.Curl.Mcp

## Goal

The defect the audit office reported as AF-0959 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0959 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `README.md:133`

Location: `README.md:133`

README.md:133 (Curl.Mcp row): "Run it with `dnx Curl.Mcp`; setup is in [its README](Curl.Mcp/README.md)." Curl.Mcp/Curl.Mcp.csproj:29 is "<PackageId>Surly.Curl.Mcp</PackageId>", Curl.Mcp/.mcp/server.json:9 is "identifier": "Surly.Curl.Mcp", and Curl.Mcp/README.md:3 and :17 give "dnx Surly.Curl.Mcp". ADR-1333 (Accepted 2026-11-21) moved every package ID to the Surly. prefix. dnx resolves a package by ID, so the root README's command names a package Curl does not publish.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path README.md,Curl.Mcp/README.md,Curl.Mcp/Curl.Mcp.csproj -Pattern 'dnx (Surly\.)?Curl\.Mcp|<PackageId>'
```

- Expected: Every dnx command names the package ID Surly.Curl.Mcp.
- Actual: README.md:133 'Run it with `dnx Curl.Mcp`' while Curl.Mcp.csproj:29 has <PackageId>Surly.Curl.Mcp</PackageId> and Curl.Mcp/README.md says 'dnx Surly.Curl.Mcp'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
