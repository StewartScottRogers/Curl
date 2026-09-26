---
id: BL-005
title: Decide how curl's upstream test cases are driven from .NET
priority: Normal
assignee: Stewart
pipeline: docs
depends-on: []
requirement: none
created: 2026-09-25
completed:
---
# BL-005 — Decide how curl's 2,126 upstream test cases are driven from .NET

## Goal

There is an agreed approach for running curl's upstream test cases against Curl,
recorded as an Architecture Decision Record.

## Context

Open question 3 in `Documentation/Product/Product-Overview.md`. The answer determines
the conformance harness, and it shapes how `conformance-auditor` checks
behaviour. Claude can research the options and draft a proposed ADR, but choosing
one is Stewart's call.

## Acceptance criteria

- [ ] Stewart has chosen an approach.
- [ ] An ADR under `Documentation/Planning/Decisions/` records the choice, the
      alternatives considered, and why.

## Notes

If a researched proposal would help, file a separate task assigned to Claude with
`pipeline: docs` to draft a proposed ADR, and add it to this task's `depends-on`.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
