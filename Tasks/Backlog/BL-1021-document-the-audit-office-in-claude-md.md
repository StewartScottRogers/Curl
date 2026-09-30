---
id: BL-1021
title: Document the audit office in CLAUDE.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1018, BL-1020]
touches: [CLAUDE.md, Audit/README.md]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1021 — Document the audit office in CLAUDE.md

## Goal

`CLAUDE.md` has an "Audit office" section that tells every session - interactive or lane - what the audit office is, what it may and may not touch, how an audit is started, and how findings become work, and `Audit/README.md`'s folder map is current.

## Context

Interactive only (`lane: no`): it writes `Audit/README.md`, and `CLAUDE.md` must describe
the tooling as built, which lanes cannot read. Run it with `/task-run BL-1021` after the
whole office exists. Design: the ADR from BL-994.

The section (after "Dark factory", before "Repository layout") states, true of the code
as it is when this runs:

- What it is: six read-only auditors (name each, one line each, with its model) and the
  seeder, auditing the factory and its code between shifts; cite the ADR.
- Audit paths (`Audit/`, `.claude/agents/audit-*`) and the four guards: interactive-only
  tasks and `task-board.ps1`'s refusals, the PreToolUse hook and
  `CURL_DARK_FACTORY_LANE`, the CI `audit-guard` job, and the `audit` branch whose pull
  requests only Stewart merges. A lane that meets an audit path stops and leaves it to an
  interactive session.
- Running one: `Audit\RunAudit.cmd -NewTab` (in herdr, like the factory; never
  `Start-Process`), its main parameters, and that it refuses during a shift.
- Cadence: on demand, before each roadmap-milestone merge to `master`, after changes to
  `RunDarkFactory.ps1`; `Audit\Tools\Test-AuditDue.ps1` says when one is due, and the
  shift's end announces it (BL-1022).
- Findings and triage: statuses, only Stewart accepts or rejects, `Audit/Triage.md` and
  `New-TasksFromAcceptedFindings.ps1`, and that a finding closes only on a confirming
  re-audit.
- Planted defects and what "unreliable" means on a scorecard.

Also: add `Audit/` to "Repository layout"'s tree beside `Tasks/` and `Documentation/`;
in "Git and GitHub", say the `audit` branch's pull requests to `master` need Stewart's
approval each time (no standing exception). Update `Audit/README.md` so no built folder
is still marked planned.

## Acceptance criteria

- [ ] `CLAUDE.md` has a `## Audit office` section between `## Dark factory` and `## Repository layout` containing every point above.
- [ ] Every command, path, parameter and environment variable the section names exists in the repository as named (checked one by one; list them under Notes).
- [ ] "Repository layout" shows `Audit/`, and "Git and GitHub" states the `audit` branch rule.
- [ ] `Audit/README.md` marks nothing as planned that exists.

## Notes

## Log

- 2026-09-29: Created.
