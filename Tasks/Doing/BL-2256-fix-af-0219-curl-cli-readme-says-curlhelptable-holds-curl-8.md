---
id: BL-2256
title: Fix AF-0219: Curl.Cli README says CurlHelpTable holds curl 8.21.0's 274 help rows; it holds 278, four of them httpsig rows 8.21.0 lacks
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2256 — Fix AF-0219: Curl.Cli README says CurlHelpTable holds curl 8.21.0's 274 help rows; it holds 278, four of them httpsig rows 8.21.0 lacks

## Goal

The defect the audit office reported as AF-0219 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0219 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0219-curl-cli-readme-says-curlhelptable-holds-curl-8-21.md`.

Location: `Curl.Cli.UnitLibrary/README.md:23`

Location: `Curl.Cli.UnitLibrary/README.md:23`

README.md line 23: '`CurlHelpTable` | curl 8.21.0's 274 help rows (option as shown, description, `CurlHelpCategories`), copied from its generated `src/tool_listhelp.c`'. Curl.Cli.UnitLibrary/CurlHelpTable.cs has 278 row lines ('        new("...'), including lines 103-106 for --httpsig-algo, --httpsig-headers, --httpsig-key and --httpsig-keyid. Those rows were added for a curl 8.22.0 built with --enable-httpsig (ADR-0487), so the count and the 'curl 8.21.0' source are both stale.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Cli.UnitLibrary/CurlHelpTable.cs -Pattern '^        new\("').Count; (Select-String -Path Curl.Cli.UnitLibrary/README.md -Pattern '274 help rows').Count
```

- Expected: The README's row count equals the table's: 274 rows, or the README states 278 and their source.
- Actual: 278 / 1

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
