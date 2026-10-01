---
id: BL-1011
title: Write the audit-performance auditor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-1007]
touches: [.claude/agents/audit-performance.md, Audit/Instructions/Performance.md]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1011 — Write the audit-performance auditor

## Goal

A read-only `audit-performance` agent measures Curl's native AOT build against real curl on the same transfers, explains every scenario where Curl is markedly slower or larger, and reports in the audit report format.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1011`. Design: the ADR from BL-994 (Sonnet). Rules and format:
`Audit/Instructions/Auditor-Rules.md` and `Report-Format.md` (BL-1001).

Two files:

- `.claude/agents/audit-performance.md` - `name: audit-performance`, `description`,
  `tools: Read, Grep, Glob, Bash`, `model: sonnet`; body points to the instructions.
- `Audit/Instructions/Performance.md` - the method:
  1. Run `Audit/Tools/Measure-Performance.ps1` (BL-1007) with `-Iterations 20`. A
     publish failure is a Critical finding (the native AOT binary is what ships).
  2. Thresholds that make a finding (state them in the file so scorecards compare like
     with like): Curl's median over 2x real curl's in any scenario is High, over 1.25x
     is Medium; median peak working set over 2x is Medium; startup over 100 ms more than
     curl's is Medium.
  3. For each finding, find the cause in the code before reporting: allocation in a
     per-byte or per-chunk loop, synchronous I/O, buffer sizes, reflection or startup
     work on the `--version` path, `Thread.Sleep` or polling. The evidence names the
     file and line; the reproduction is the scenario's `Measure-Performance.ps1`
     command.
  4. Also check the AOT publish output for trim or AOT warnings (`IL2xxx`, `IL3xxx`);
     each is a Medium finding.
  5. Put every scenario's numbers in `metrics` with the names `Report-Format.md` defines,
     even when there is no finding, so scorecards show the trend.

## Acceptance criteria

- [x] `.claude/agents/audit-performance.md` exists with `name: audit-performance`, `model: sonnet`, `tools: Read, Grep, Glob, Bash` and no editing tool.
- [x] `Audit/Instructions/Performance.md` states the five steps and the numeric thresholds above.
- [x] `claude agents` lists `audit-performance`.
- [x] A trial run with `-Iterations 3` in a detached worktree ends with one report block that parses with `ConvertFrom-Json` and carries every performance metric name; command and summary under Notes.

## Notes

- Audit branch commit 578f5207 (agent, Performance.md; Measure-Performance.ps1 now keeps publish.log for step 4; the Audit README's merge paragraph follows Stewart's standing exception).
- Trial: detached worktree of 578f5207, fingerprint 090b08f4...; claude -p --agent audit-performance --dangerously-skip-permissions "Audit the tree ... use -Iterations 3 ...". One json block, all six fields, all 37 performance metric names. Two High findings: chunked 2.7x curl (one WriteAsync per decoded chunk, HttpResponseBodyReader.cs:313, probable cause), headers-verbose 2.5x (cause not found); memory under 2x everywhere; startup faster than curl; no IL2/IL3 warnings. Worktree unchanged; removed after.
- claude agents: in Claude Code 2.1.284 it lists running sessions, not agent definitions, so it cannot show this agent; the trial's claude -p --agent <name> is the proof the agent is found.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The read-only audit-performance auditor measures Curl against curl with fixed thresholds and traces findings to the code; on the audit branch.
