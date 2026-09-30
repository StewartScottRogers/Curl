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
completed:
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

- [ ] `.claude/agents/audit-performance.md` exists with `name: audit-performance`, `model: sonnet`, `tools: Read, Grep, Glob, Bash` and no editing tool.
- [ ] `Audit/Instructions/Performance.md` states the five steps and the numeric thresholds above.
- [ ] `claude agents` lists `audit-performance`.
- [ ] A trial run with `-Iterations 3` in a detached worktree ends with one report block that parses with `ConvertFrom-Json` and carries every performance metric name; command and summary under Notes.

## Notes

## Log

- 2026-09-29: Created.
