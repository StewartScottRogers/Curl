---
id: AF-0135
title: CLAUDE.md says audit-quality, audit-truthfulness and audit-process run on Sonnet; their agent files say opus
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:CLAUDE.md:audit-agent-models:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0135 - CLAUDE.md says audit-quality, audit-truthfulness and audit-process run on Sonnet; their agent files say opus

## Summary

Medium finding from the truthfulness auditor at `CLAUDE.md:173`: CLAUDE.md says audit-quality, audit-truthfulness and audit-process run on Sonnet; their agent files say opus. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `CLAUDE.md:173`

CLAUDE.md lines 173, 177 and 178: '`audit-quality` (Sonnet)', '`audit-truthfulness` (Sonnet)', '`audit-process` (Sonnet)'. The agent definitions say otherwise: .claude/agents/audit-quality.md 'model: opus', .claude/agents/audit-truthfulness.md 'model: opus', .claude/agents/audit-process.md 'model: opus'. The other statements in that list (audit-security Opus, audit-performance Sonnet, audit-conformance Opus, audit-seeder Sonnet, every gap-* model) match their files. An agent sizing an audit's cost or choosing models from CLAUDE.md is wrong about three of the six auditors.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '`audit-(quality|truthfulness|process)` \((\w+)\)'; Select-String -Path .claude/agents/audit-quality.md,.claude/agents/audit-truthfulness.md,.claude/agents/audit-process.md -Pattern '^model:'
```

- Expected: The model CLAUDE.md names for each auditor equals the agent file's model: line.
- Actual: CLAUDE.md:173/177/178 say (Sonnet); audit-quality.md, audit-truthfulness.md and audit-process.md say 'model: opus'.

## Re-audits

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
