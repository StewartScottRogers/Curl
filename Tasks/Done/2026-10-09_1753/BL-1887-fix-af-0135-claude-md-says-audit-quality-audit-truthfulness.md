---
id: BL-1887
title: Fix AF-0135: CLAUDE.md says audit-quality, audit-truthfulness and audit-process run on Sonnet; their agent files say opus
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1887 — Fix AF-0135: CLAUDE.md says audit-quality, audit-truthfulness and audit-process run on Sonnet; their agent files say opus

## Goal

The defect the audit office reported as AF-0135 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0135 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0135-claude-md-says-audit-quality-audit-truthfulness-an.md`.

Location: `CLAUDE.md:173`

Location: `CLAUDE.md:173`

CLAUDE.md lines 173, 177 and 178: '`audit-quality` (Sonnet)', '`audit-truthfulness` (Sonnet)', '`audit-process` (Sonnet)'. The agent definitions say otherwise: .claude/agents/audit-quality.md 'model: opus', .claude/agents/audit-truthfulness.md 'model: opus', .claude/agents/audit-process.md 'model: opus'. The other statements in that list (audit-security Opus, audit-performance Sonnet, audit-conformance Opus, audit-seeder Sonnet, every gap-* model) match their files. An agent sizing an audit's cost or choosing models from CLAUDE.md is wrong about three of the six auditors.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '`audit-(quality|truthfulness|process)` \((\w+)\)'; Select-String -Path .claude/agents/audit-quality.md,.claude/agents/audit-truthfulness.md,.claude/agents/audit-process.md -Pattern '^model:'
```

- Expected: The model CLAUDE.md names for each auditor equals the agent file's model: line.
- Actual: CLAUDE.md:173/177/178 say (Sonnet); audit-quality.md, audit-truthfulness.md and audit-process.md say 'model: opus'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- CLAUDE.md lines 173, 177, 178 now say (Opus). The factory's audit guard stops a lane reading `.claude/agents/audit-*`, so the agent files' `model: opus` lines were taken from the finding, not re-read; the re-audit confirms them.
- Only CLAUDE.md changed (no .cs or project file), so build and tests are unaffected; they were not rerun.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. CLAUDE.md names Opus for audit-quality, audit-truthfulness and audit-process, matching the finding's agent files
