---
id: BL-005
title: Decide how curl's upstream test cases are driven from .NET
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
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

- [x] Stewart has chosen an approach.
- [ ] An ADR under `Documentation/Planning/Decisions/` records the choice, the
      alternatives considered, and why.
- [ ] Open question 3 in `Documentation/Product/Product-Overview.md` is marked answered, pointing at the ADR.
- [ ] Claude tasks to build the MSTest conformance harness are filed in `Tasks/Backlog`, each one `/task-run` in size and depending on this one.

## Notes

**Decision (Stewart, 2026-09-26):** Port curl's upstream test cases to MSTest: convert the test files into data-driven MSTest cases run in .NET, with no Perl and no `runtests.pl`. No third-party test library (CLAUDE.md).

If a researched proposal would help, file a separate task assigned to Claude with
`pipeline: docs` to draft a proposed ADR, and add it to this task's `depends-on`.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
- 2026-09-26: Stewart chose porting the test cases to MSTest. Reassigned to Claude to record the ADR.
- 2026-09-26: Backlog -> Doing.
