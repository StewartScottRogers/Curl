---
id: BL-1965
title: Fix AF-0146: CLAUDE.md's layout says 71 projects; the root holds 73 Curl.* projects
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-10
completed:
---
# BL-1965 — Fix AF-0146: CLAUDE.md's layout says 71 projects; the root holds 73 Curl.* projects

## Goal

The defect the audit office reported as AF-0146 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0146 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0146-claude-md-s-layout-says-71-projects-the-root-holds.md`.

Location: `CLAUDE.md:253`

Location: `CLAUDE.md:253`

The Repository layout tree in the root CLAUDE.md reads '├── ...  ← 71 projects, one flat alphabetical run'. The repository root holds 73 Curl.* directories, each with a .csproj (Curl.slnx lists the same 73 plus 5 shared projects), so the count is stale by two (for example Curl.Conformance.SshServer.UnitLibrary and its UnitTests twin).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat' | ForEach-Object { $_.Line.Trim() }; @(Get-ChildItem -Directory -Filter 'Curl.*' | Where-Object { Get-ChildItem $_.FullName -Filter *.csproj }).Count
```

- Expected: The number in the CLAUDE.md line equals the count printed after it.
- Actual: '... 71 projects, one flat alphabetical run' then 73

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
