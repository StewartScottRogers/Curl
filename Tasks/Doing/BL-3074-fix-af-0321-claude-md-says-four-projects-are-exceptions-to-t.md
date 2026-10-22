---
id: BL-3074
title: Fix AF-0321: CLAUDE.md says four projects are exceptions to the Curl.<Area>.UnitLibrary rule; Curl.Config is a fifth
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3074 — Fix AF-0321: CLAUDE.md says four projects are exceptions to the Curl.<Area>.UnitLibrary rule; Curl.Config is a fifth

## Goal

The defect the audit office reported as AF-0321 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0321 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0321-claude-md-says-four-projects-are-exceptions-to-the.md`.

Location: `CLAUDE.md:310`

Location: `CLAUDE.md:310`

CLAUDE.md:310-314: 'Four projects are exceptions to the `Curl.<Area>.UnitLibrary` rule: the executable `Curl.Console` ..., the two programming interfaces `Curl.Api` and `Libcurl` ..., and the MCP server host `Curl.Mcp`.' The repository root also has Curl.Config/Curl.Config.csproj (OutputType Exe, AssemblyName curl-config, ADR-0712), listed in Curl.slnx:213. Curl.Config/CLAUDE.md calls itself 'a naming exception, like Curl.Console and Curl.Mcp'. An agent applying the root rule would treat Curl.Config as misnamed.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern 'Four projects are exceptions'; Get-ChildItem -Directory | Where-Object { (Test-Path (Join-Path $_.FullName ($_.Name + '.csproj'))) -and $_.Name -notmatch '\.(UnitLibrary|UnitTests|IntegrationTests)$' } | ForEach-Object Name
```

- Expected: The count in CLAUDE.md matches the projects listed: Curl.Api, Curl.Config, Curl.Console, Curl.Mcp, Libcurl (five), or CLAUDE.md names Curl.Config.
- Actual: CLAUDE.md:310 says 'Four projects are exceptions'; the directory list prints Curl.Api, Curl.Config, Curl.Console, Curl.Mcp, Libcurl.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
