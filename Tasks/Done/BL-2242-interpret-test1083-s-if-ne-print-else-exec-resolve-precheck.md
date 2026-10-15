---
id: BL-2242
title: Interpret test1083's if-ne-print-else-exec RESOLVE precheck one-liner so the case runs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2242 — Interpret test1083's if-ne-print-else-exec RESOLVE precheck one-liner so the case runs

## Goal

test1083 is no longer skipped for its precheck: the harness interprets `%PERL -e "if('A' ne 'B') {print 'TEXT';} else {exec '%RESOLVE --ipv6 NAME'; print '...';}"`.

## Context

Found by BL-2227, which gave `%CLIENT6IP` the value `[::1]`: test1083 (`HTTP-IPv6 GET with ip6-localhost --interface`) now expands its precheck to `perl -e "if('[::1]' ne '[::1]') {print 'Test requires default test client host address';} else {exec 'resolve --ipv6 ip6-localhost'; print 'Cannot run precheck resolve';}"`, which `UpstreamPerlOneLiner` (Curl.Conformance.UnitLibrary) does not interpret, so the case skips with "the harness does not interpret the <client><precheck> line". Add a form: when A and B differ print TEXT (exit 0), else run the exec'd line through `UpstreamResolveCheck.RunLine` and return its result (`exec` replaces the process, so the trailing print never runs). The library CLAUDE.md says test1083's `exec '%RESOLVE ...'` form is not interpreted; update it.

## Acceptance criteria

- [x] `UpstreamPerlOneLinerTests` (or `UpstreamPerlCheckLineTests`) pin both branches of the new form.
- [x] test1083 runs; if it passes it is on `PassingUpstreamCases.txt`, otherwise its first difference is named under Notes with a follow-up task.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; fast tests green; library `CLAUDE.md` updated.

## Notes

Added form to UpstreamPerlOneLiner; exec'd line restricted to resolve lines and answered by UpstreamResolveCheck. test1083 passes and is on PassingUpstreamCases.txt. Quality run: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. UpstreamPerlOneLiner interprets test1083's if-ne-print-else-exec-resolve precheck; test1083 passes
