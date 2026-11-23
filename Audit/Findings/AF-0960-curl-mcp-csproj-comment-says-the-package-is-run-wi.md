---
id: AF-0960
title: Curl.Mcp.csproj comment says the package is run with 'dnx Curl.Mcp'; its PackageId is Surly.Curl.Mcp
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Mcp/Curl.Mcp.csproj:PackageComment:false-statement
reproduction: none
task: BL-5729
tasks: BL-5729
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1351.md
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

- 2026-11-22 | 2026-11-22_0210.md | reproduces: no | Ran Select-String -Path Curl.Mcp/Curl.Mcp.csproj -Pattern 'dnx Curl\.Mcp|<PackageId>'. The only match is line 29, <PackageId>Surly.Curl.Mcp</PackageId>; the comment at line 11 now reads 'tool package of PackageType McpServer that `dnx Surly.Curl.Mcp` runs', so it agrees with the PackageId.
- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the reproduction: the only match is Curl.Mcp.csproj:29 <PackageId>Surly.Curl.Mcp</PackageId>. The comment at line 11 now reads 'dnx Surly.Curl.Mcp', so 'dnx Curl\.Mcp' does not match.

## Log

- 2026-11-21: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
