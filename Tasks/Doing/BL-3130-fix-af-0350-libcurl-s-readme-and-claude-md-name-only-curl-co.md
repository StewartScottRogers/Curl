---
id: BL-3130
title: Fix AF-0350: Libcurl's README and CLAUDE.md name only Curl.Console and Curl.Api beside it as naming exceptions; Curl.Config and Curl.Mcp are too
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Libcurl]
requirement: none
created: 2026-10-22
completed:
---
# BL-3130 — Fix AF-0350: Libcurl's README and CLAUDE.md name only Curl.Console and Curl.Api beside it as naming exceptions; Curl.Config and Curl.Mcp are too

## Goal

The defect the audit office reported as AF-0350 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0350 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0350-libcurl-s-readme-and-claude-md-name-only-curl-cons.md`.

Location: `Libcurl/README.md:5`

Location: `Libcurl/README.md:5`

Libcurl/README.md line 5 says: 'with `Curl.Console` and `Curl.Api` it is a naming exception', and Libcurl/CLAUDE.md line 4 says: 'A naming exception, with `Curl.Console` and `Curl.Api`.' The root CLAUDE.md says five projects are exceptions to the Curl.<Area>.UnitLibrary rule. The repository root holds five C# projects without a .UnitLibrary/.UnitTests/.IntegrationTests suffix: Curl.Api, Curl.Config, Curl.Console, Curl.Mcp and Libcurl. The two documents disagree with the root CLAUDE.md and leave out Curl.Config and Curl.Mcp.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Libcurl/README.md, Libcurl/CLAUDE.md -Pattern 'naming exception' | ForEach-Object { '{0}:{1}: {2}' -f $_.Filename, $_.LineNumber, $_.Line.Trim() }; Get-ChildItem -Directory | Where-Object { (Test-Path (Join-Path $_.FullName ($_.Name + '.csproj'))) -and $_.Name -notmatch '\.(UnitLibrary|UnitTests|IntegrationTests)$' } | ForEach-Object Name
```

- Expected: The exceptions the Libcurl documents name match the five projects listed (or the documents point to the root CLAUDE.md's list).
- Actual: README.md:5: `Curl.Console` and `Curl.Api` it is a naming exception. ... / CLAUDE.md:4: ... A naming exception, with `Curl.Console` and `Curl.Api`. / Curl.Api Curl.Config Curl.Console Curl.Mcp Libcurl

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
