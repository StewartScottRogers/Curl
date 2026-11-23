---
id: AF-0959
title: README.md tells readers to run the MCP server with 'dnx Curl.Mcp', but the package ID is Surly.Curl.Mcp
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
key: truthfulness:README.md:Curl.Mcp-dnx-command:false-statement
reproduction: none
task: BL-5728
tasks: BL-5728
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1351.md
---
# AF-0959 - README.md tells readers to run the MCP server with 'dnx Curl.Mcp', but the package ID is Surly.Curl.Mcp

## Summary

High finding from the truthfulness auditor at `README.md:133`: README.md tells readers to run the MCP server with 'dnx Curl.Mcp', but the package ID is Surly.Curl.Mcp.

## Evidence

Location: `README.md:133`

README.md:133 (Curl.Mcp row): "Run it with `dnx Curl.Mcp`; setup is in [its README](Curl.Mcp/README.md)." Curl.Mcp/Curl.Mcp.csproj:29 is "<PackageId>Surly.Curl.Mcp</PackageId>", Curl.Mcp/.mcp/server.json:9 is "identifier": "Surly.Curl.Mcp", and Curl.Mcp/README.md:3 and :17 give "dnx Surly.Curl.Mcp". ADR-1333 (Accepted 2026-11-21) moved every package ID to the Surly. prefix. dnx resolves a package by ID, so the root README's command names a package Curl does not publish.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path README.md,Curl.Mcp/README.md,Curl.Mcp/Curl.Mcp.csproj -Pattern 'dnx (Surly\.)?Curl\.Mcp|<PackageId>'
```

- Expected: Every dnx command names the package ID Surly.Curl.Mcp.
- Actual: README.md:133 'Run it with `dnx Curl.Mcp`' while Curl.Mcp.csproj:29 has <PackageId>Surly.Curl.Mcp</PackageId> and Curl.Mcp/README.md says 'dnx Surly.Curl.Mcp'.

## Re-audits

- 2026-11-22 | 2026-11-22_0210.md | reproduces: no | Ran Select-String -Path README.md,Curl.Mcp/README.md,Curl.Mcp/Curl.Mcp.csproj -Pattern 'dnx (Surly\.)?Curl\.Mcp|<PackageId>'. README.md:133 now says 'Run it with `dnx Surly.Curl.Mcp`'; Curl.Mcp/README.md:3 and :17 say 'dnx Surly.Curl.Mcp'; Curl.Mcp.csproj:29 has <PackageId>Surly.Curl.Mcp</PackageId>. No 'dnx Curl.Mcp' remains; the documented command matches the package ID.
- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the reproduction: README.md:133 and Curl.Mcp/README.md:3,17 now say 'dnx Surly.Curl.Mcp', and Curl.Mcp.csproj:29 has <PackageId>Surly.Curl.Mcp</PackageId>. No bare 'dnx Curl.Mcp' remains.

## Log

- 2026-11-21: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
