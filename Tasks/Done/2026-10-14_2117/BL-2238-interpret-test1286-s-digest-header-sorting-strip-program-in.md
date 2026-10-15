---
id: BL-2238
title: Interpret test1286's Digest-header sorting strip program in the upstream case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2238 — Interpret test1286's Digest-header sorting strip program in the upstream case runner

## Goal

Make the upstream case runner carry out test1286's `<strip>` program, which sorts and normalises the fields of an `Authorization: Digest` line, so case 1286 is measured.

## Context

Split from BL-2223 (2026-10-14). Strip code: `if(s/^(Authorization: Digest )([^\r\n]+)(\r?\n)$//) { $_ = $1 . join(', ', map { s/^(cnonce=)"[a-zA-Z0-9+\/=]+"$/$1REMOVED/; s/^(response=)"[a-f0-9]{32}"$/$1REMOVED/; s/^qop="auth"$/qop=auth/; $_ } sort split(/, */, $2)) . $3; }`. `UpstreamPerlSubstitution.Parse` returns null for it, so `UpstreamCaseScreening.UnsupportedStripCode` skips the case. Interpret it faithfully (no eval, no process, AOT-safe): a sibling of `UpstreamPerlSubstitution` for this split/sort/map/join form is acceptable. Perl's `sort` is ordinal (byte) order.

## Acceptance criteria

- [x] The program is interpreted exactly as Perl would on matching and non-matching lines, pinned by unit tests in `Curl.Conformance.UnitTests`.
- [x] test1286 is no longer screened out; added to `PassingUpstreamCases.txt` if it passes, or its failure named in Notes with a follow-up task.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; build and fast tests green; `Curl.Conformance.UnitLibrary/CLAUDE.md` updated.

## Notes

- 2026-10-14: `UpstreamPerlFieldSortProgram` reads the program by its whole form (test, group numbers, separator, map's substitutions, split pattern) and runs each piece as `UpstreamPerlSubstitution` / `UpstreamRegex` already do, with no `eval`; `IUpstreamPerlLineEdit.Parse` tries the plain substitution first, then the program, for screening and verification alike. Choice: parse the form generally (group numbers and separator read from the program) rather than match test1286's text exactly, since it costs nothing and stays faithful to Perl.
- 2026-10-14: test1286 is no longer screened out. Its first two requests now match after the strip; it fails at line 14, where curl sends `Authorization: Digest ... nc=00000002` on the same-host redirect to `/12860001` and Curl sends no credentials. Filed as BL-2252; test1286 is not added to `PassingUpstreamCases.txt`.
- 2026-10-14: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` showed one gap, `SortedFields` branch 75% (all fields empty); the `, , ` test row covers it.

## Log

- 2026-10-14: Created (split from BL-2223).
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. UpstreamPerlFieldSortProgram runs test1286's strip program; test1286 measured, its Curl difference filed as BL-2252
