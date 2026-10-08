---
id: BL-1676
title: Fix AF-0049: CLAUDE.md says global.json is still to be added, but it exists
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1676 — Fix AF-0049: CLAUDE.md says global.json is still to be added, but it exists

## Goal

The defect the audit office reported as AF-0049 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0049 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0049-claude-md-says-global-json-is-still-to-be-added-bu.md`.

Location: `CLAUDE.md:14`

Location: `CLAUDE.md:14`

CLAUDE.md:14 reads '.NET Software Development Kit 10 (see `global.json` once added).' global.json is at the repository root and pins {"sdk": {"version": "10.0.401", "rollForward": "latestFeature"}}. The statement is stale: it calls the file future work and does not point at the SDK pin that exists.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path CLAUDE.md -Pattern 'global.json. once added').Line; Test-Path global.json
```

- Expected: No match for 'once added' (the line points at global.json as it is), then True.
- Actual: - .NET Software Development Kit 10 (see `global.json` once added). Target framework: `net10.0` unless a project states otherwise.  then  True

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Done directly: a one-line doc fix, no align-and-document run needed. CLAUDE.md:14 now names `global.json` and the SDK pin it holds (10.0.401, rollForward latestFeature). Reproduction prints only `True`. Build clean, fast tests green.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. CLAUDE.md now points at the existing global.json SDK pin instead of calling it future work
