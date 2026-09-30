---
id: BL-1012
title: Write the audit-conformance auditor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-1006]
touches: [.claude/agents/audit-conformance.md, Audit/Instructions/Conformance.md]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1012 — Write the audit-conformance auditor

## Goal

A read-only `audit-conformance` agent runs differential tests of real curl against Curl on generated command lines, classifies each difference, and reports in the audit report format.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1012`. Design: the ADR from BL-994 (Opus). Rules and format:
`Audit/Instructions/Auditor-Rules.md` and `Report-Format.md` (BL-1001).

It builds on the existing `.claude/agents/conformance-auditor.md` without duplicating
it: that agent is the factory's own on-demand check of one area (it is used by the
`/feature` and `/protocol` pipelines); this one is the independent periodic check. The
instruction file points to `conformance-auditor.md`'s "Establish the upstream truth
first" and "What to compare" sections as the standard to apply, and its severity words
(Blocker/Major/Minor) map to the finding scale as Blocker -> High (Critical when it
changes an exit code on a common path), Major -> Medium, Minor -> Low. Do not change
`conformance-auditor.md`.

Two files:

- `.claude/agents/audit-conformance.md` - `name: audit-conformance`, `description`,
  `tools: Read, Grep, Glob, Bash`, `model: opus`; body points to the instructions.
- `Audit/Instructions/Conformance.md` - the method:
  1. Run `Audit/Tools/Invoke-DifferentialConformance.ps1` (BL-1006) with `-Count` from
     the prompt (default 300) and a fresh seed recorded in the report.
  2. For each differing case, reduce it: drop options one at a time while the
     difference persists, so the finding names the smallest command line.
  3. Group cases with the same cause into one finding (one `key` per cause).
  4. Phase 2 only: check whether an ADR records the divergence as deliberate, and
     annotate the finding with it; an undocumented divergence stays a finding.
  5. State the reference curl version in every finding.

## Acceptance criteria

- [ ] `.claude/agents/audit-conformance.md` exists with `name: audit-conformance`, `model: opus`, `tools: Read, Grep, Glob, Bash` and no editing tool.
- [ ] `Audit/Instructions/Conformance.md` states the five steps, cites `conformance-auditor.md`'s sections as the standard and gives the severity mapping above.
- [ ] `git diff --quiet HEAD -- .claude/agents/conformance-auditor.md` succeeds (the existing agent is unchanged).
- [ ] `claude agents` lists `audit-conformance`.
- [ ] A trial run with `-Count 10` in a detached worktree ends with one report block that parses with `ConvertFrom-Json`; command and summary under Notes.

## Notes

## Log

- 2026-09-29: Created.
