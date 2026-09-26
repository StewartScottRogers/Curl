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
completed: 2026-09-26
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
- [x] An ADR under `Documentation/Planning/Decisions/` records the choice, the
      alternatives considered, and why.
- [x] Open question 3 in `Documentation/Product/Product-Overview.md` is marked answered, pointing at the ADR.
- [x] Claude tasks to build the MSTest conformance harness are filed in `Tasks/Backlog`, each one `/task-run` in size and depending on this one.

## Notes

**Decision (Stewart, 2026-09-26):** Port curl's upstream test cases to MSTest: convert the test files into data-driven MSTest cases run in .NET, with no Perl and no `runtests.pl`. No third-party test library (CLAUDE.md).

If a researched proposal would help, file a separate task assigned to Claude with
`pipeline: docs` to draft a proposed ADR, and add it to this task's `depends-on`.

**Recorded (Claude, 2026-09-26):** ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).
Harness design choices made under Stewart's delegation, and why:
- In process through `CurlComposition.CreateRunner` with in-memory server emulations behind
  `IConnector`, not the binary against loopback servers: keeps the fast suite off the network
  and needs no publish step; differential testing still covers the process boundary.
- Test data vendored and pinned to `curl-8_21_0`, not fetched at test time: no network in tests
  and a pass rate stated against a named release.
- A committed passing-case list as a ratchet, other cases `Inconclusive`: the suite stays green
  while the pass rate climbs, and a passing case cannot silently regress.
- libcurl `<tool>` cases are out of scope: Curl replaces the tool, not the library.

Harness tasks filed: BL-148 (scaffold projects), BL-143 (vendor test data), BL-144 (parser),
BL-145 (variables and `%if`), BL-146 (sws HTTP emulation), BL-147 (data-driven runner and
ratchet). Each lists BL-005 in `depends-on`. Other server emulations are filed after BL-147.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
- 2026-09-26: Stewart chose porting the test cases to MSTest. Reassigned to Claude to record the ADR.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ADR-0013 records porting upstream cases to in-process data-driven MSTest; harness tasks BL-148..BL-147 filed
