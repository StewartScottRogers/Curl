---
id: AF-0303
title: README says the only dependency is the .NET base class library; Curl.Mcp references two NuGet packages
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:README.md:OnlyDependencyIsTheBcl:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0303 - README says the only dependency is the .NET base class library; Curl.Mcp references two NuGet packages

## Summary

Low finding from the truthfulness auditor at `README.md:48`: README says the only dependency is the .NET base class library; Curl.Mcp references two NuGet packages.

## Evidence

Location: `README.md:48`

README.md lines 48-49, 'What this is': 'The only dependency is the .NET base class library.' Curl.Mcp/Curl.Mcp.csproj lines 54-55 reference Microsoft.Extensions.Hosting and ModelContextProtocol, and Directory.Packages.props pins them (lines 24-25). The same README's table lists Curl.Mcp as one of its projects. CLAUDE.md records the exception (Stewart, 2026-10-15, ADR-0548). The statement is true of the curl port's libraries but not of the solution the README describes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path README.md -Pattern 'The only dependency is the .NET base' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }; Select-String -Path Curl.Mcp/Curl.Mcp.csproj -Pattern 'PackageReference' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: Either the README names Curl.Mcp's two approved packages as the exception, or Curl.Mcp.csproj has no PackageReference.
- Actual: 48: driven from a unit test without touching a network. The only dependency is the .NET base
54: <PackageReference Include="Microsoft.Extensions.Hosting" />
55: <PackageReference Include="ModelContextProtocol" />

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
