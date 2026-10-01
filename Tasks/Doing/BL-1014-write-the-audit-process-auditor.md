---
id: BL-1014
title: Write the audit-process auditor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-1008]
touches: [.claude/agents/audit-process.md, Audit/Instructions/Process.md]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1014 — Write the audit-process auditor

## Goal

A read-only `audit-process` agent audits how the dark factory works - time per task, redone work, time with CI red, idle lanes, overlap-caused serialisation and token cost - from its logs and git history, and reports the causes it can evidence in the audit report format.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1014`. Design: the ADR from BL-994 (Sonnet). Rules and format:
`Audit/Instructions/Auditor-Rules.md` and `Report-Format.md` (BL-1001). Its subject is
the factory's logs (`<repo>.logs`, e.g. `Z:\repos\Curl.logs`), git history and CI runs,
so it reads those in phase 1.

Two files:

- `.claude/agents/audit-process.md` - `name: audit-process`, `description`,
  `tools: Read, Grep, Glob, Bash`, `model: sonnet`; body points to the instructions.
- `Audit/Instructions/Process.md` - the method:
  1. Run `Audit/Tools/Measure-FactoryProcess.ps1` (BL-1008) with `-Since` the previous
     scorecard's date (given in the prompt; default 7 days ago) and put every metric in
     the report's `metrics`.
  2. Findings, each with the tasks, lanes and times as evidence:
     - a task claimed three or more times, or requeued twice (redone work) - find why
       from its `Log` and run logs;
     - CI red for over 60 minutes in total, or any single red spell over 30 minutes;
     - `waitOverlapMinutes` over 20% of lane time: name the `touches` that serialised
       lanes (often a shared file such as `RunDarkFactory.ps1` or `Curl.slnx`) and
       whether a narrower `touches` would have been true;
     - `waitNothingReadyMinutes` over 20% with ready tasks assigned to Stewart or
       interactive only;
     - the costliest 5 tasks by `costUsd`, when one cost over 3x the median;
     - runs that ended without `FACTORY: DONE` or `FACTORY: BLOCKED`.
  3. Do not judge the code the tasks produced; the other auditors do that.
  Severity: lost work or a merge of red CI is High; waste over 20% of lane time is
  Medium; the rest Low.

## Acceptance criteria

- [ ] `.claude/agents/audit-process.md` exists with `name: audit-process`, `model: sonnet`, `tools: Read, Grep, Glob, Bash` and no editing tool.
- [ ] `Audit/Instructions/Process.md` states the three steps, each finding rule with its threshold, and the severity rule above.
- [ ] `claude agents` lists `audit-process`.
- [ ] A trial run with `-Since` one day back ends with one report block that parses with `ConvertFrom-Json` and carries every process metric name; command and summary under Notes; the log folder is unchanged.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
