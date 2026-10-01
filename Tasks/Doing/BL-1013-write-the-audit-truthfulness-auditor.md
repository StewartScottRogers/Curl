---
id: BL-1013
title: Write the audit-truthfulness auditor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001]
touches: [.claude/agents/audit-truthfulness.md, Audit/Instructions/Truthfulness.md]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1013 — Write the audit-truthfulness auditor

## Goal

A read-only `audit-truthfulness` agent samples names, documents and ADRs, checks each against what the code does, and reports every statement that is false of the code as it is now - without fixing anything.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1013`. Design: the ADR from BL-994 (Sonnet). Rules and format:
`Audit/Instructions/Auditor-Rules.md` and `Report-Format.md` (BL-1001).

The rule it checks is CLAUDE.md's "Say what it does, do what it says". The factory's
own `.claude/agents/align-and-document.md` owns that rule and fixes drift under tasks;
this auditor does not duplicate it: it never edits, it samples independently, and its
findings become tasks that `align-and-document` (or a `docs` pipeline run) fixes. The
instruction file says so, and points to `align-and-document.md` for the definition of a
misaligned name, rather than restating it.

Two files:

- `.claude/agents/audit-truthfulness.md` - `name: audit-truthfulness`, `description`,
  `tools: Read, Grep, Glob, Bash`, `model: sonnet`; body points to the instructions.
- `Audit/Instructions/Truthfulness.md` - the method, each with a sample size so audits
  are comparable:
  1. Names: 30 public types and 30 public methods chosen with a seed recorded in the
     report, across all `*.UnitLibrary` projects and `Curl.Console`; does each do what
     its name says and nothing its name hides?
  2. XML doc comments on the same members: is every statement true of the code?
  3. Documents: `README.md`, `CLAUDE.md`, every project's `CLAUDE.md`/`README.md`, and
     `Documentation/Product/Requirements.md`: 20 checkable statements, each verified
     against the code or by running it.
  4. ADRs: the 10 most recent Accepted ADRs; does the code still do what each decided?
     (ADRs are this auditor's subject, so it reads them in phase 1 as claims to check.)
  5. Scripts: `RunDarkFactory.ps1`'s and `task-board.ps1`'s comment-based help against
     their behaviour, 10 statements each.
  Severity: a false statement an agent would act on wrongly (a wrong exit code, a
  wrong file, a wrong rule) is High; a misleading name is Medium; imprecision is Low.

## Acceptance criteria

- [ ] `.claude/agents/audit-truthfulness.md` exists with `name: audit-truthfulness`, `model: sonnet`, `tools: Read, Grep, Glob, Bash` and no editing tool.
- [ ] `Audit/Instructions/Truthfulness.md` states the five steps with their sample sizes, the severity rule, and its relationship to `align-and-document` (reports only; fixing stays with `align-and-document`).
- [ ] `git diff --quiet HEAD -- .claude/agents/align-and-document.md` succeeds.
- [ ] `claude agents` lists `audit-truthfulness`.
- [ ] A trial run limited to step 5 in a detached worktree ends with one report block that parses with `ConvertFrom-Json`; command and summary under Notes.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
