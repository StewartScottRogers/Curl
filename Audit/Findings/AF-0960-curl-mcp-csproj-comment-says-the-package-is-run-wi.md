---
id: AF-0960
title: Curl.Mcp.csproj comment says the package is run with 'dnx Curl.Mcp'; its PackageId is Surly.Curl.Mcp
auditor: truthfulness
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Mcp/Curl.Mcp.csproj:PackageComment:false-statement
reproduction: none
task: BL-5729
tasks: BL-5729
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0960 - Curl.Mcp.csproj comment says the package is run with 'dnx Curl.Mcp'; its PackageId is Surly.Curl.Mcp

## Summary

Low finding from the truthfulness auditor at `Curl.Mcp/Curl.Mcp.csproj:11`: Curl.Mcp.csproj comment says the package is run with 'dnx Curl.Mcp'; its PackageId is Surly.Curl.Mcp.

## Evidence

Location: `Curl.Mcp/Curl.Mcp.csproj:11`

Lines 10-11: "a .NET tool package of PackageType McpServer that `dnx Curl.Mcp` runs". The same file's line 29 sets <PackageId>Surly.Curl.Mcp</PackageId> (ADR-1333). The comment was not updated with the rename.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Mcp/Curl.Mcp.csproj -Pattern 'dnx Curl\.Mcp|<PackageId>'
```

- Expected: The comment names the package ID it sets: dnx Surly.Curl.Mcp.
- Actual: 11: ...that `dnx Curl.Mcp` runs...; 29: <PackageId>Surly.Curl.Mcp</PackageId>

## Re-audits

## Log

- 2026-11-21: filed proposed.
