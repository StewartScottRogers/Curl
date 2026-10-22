---
id: BL-2941
title: Fix AF-0303: README says the only dependency is the .NET base class library; Curl.Mcp references two NuGet packages
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2941 — Fix AF-0303: README says the only dependency is the .NET base class library; Curl.Mcp references two NuGet packages

## Goal

The defect the audit office reported as AF-0303 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0303 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0303-readme-says-the-only-dependency-is-the-net-base-cl.md`.

Location: `README.md:48`

Location: `README.md:48`

README.md lines 48-49, 'What this is': 'The only dependency is the .NET base class library.' Curl.Mcp/Curl.Mcp.csproj lines 54-55 reference Microsoft.Extensions.Hosting and ModelContextProtocol, and Directory.Packages.props pins them (lines 24-25). The same README's table lists Curl.Mcp as one of its projects. CLAUDE.md records the exception (Stewart, 2026-10-15, ADR-0548). The statement is true of the curl port's libraries but not of the solution the README describes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path README.md -Pattern 'The only dependency is the .NET base' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }; Select-String -Path Curl.Mcp/Curl.Mcp.csproj -Pattern 'PackageReference' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: Either the README names Curl.Mcp's two approved packages as the exception, or Curl.Mcp.csproj has no PackageReference.
- Actual: 48: driven from a unit test without touching a network. The only dependency is the .NET base
54: <PackageReference Include="Microsoft.Extensions.Hosting" />
55: <PackageReference Include="ModelContextProtocol" />

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

README now says the curl port's only dependency is the BCL and names Curl.Mcp's two approved packages as the exception. Docs-only; build clean, fast tests green.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. README names Curl.Mcp's packages as the exception
