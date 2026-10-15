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
completed:
---
# BL-2238 — Interpret test1286's Digest-header sorting strip program in the upstream case runner

## Goal

Make the upstream case runner carry out test1286's `<strip>` program, which sorts and normalises the fields of an `Authorization: Digest` line, so case 1286 is measured.

## Context

Split from BL-2223 (2026-10-14). Strip code: `if(s/^(Authorization: Digest )([^\r\n]+)(\r?\n)$//) { $_ = $1 . join(', ', map { s/^(cnonce=)"[a-zA-Z0-9+\/=]+"$/$1REMOVED/; s/^(response=)"[a-f0-9]{32}"$/$1REMOVED/; s/^qop="auth"$/qop=auth/; $_ } sort split(/, */, $2)) . $3; }`. `UpstreamPerlSubstitution.Parse` returns null for it, so `UpstreamCaseScreening.UnsupportedStripCode` skips the case. Interpret it faithfully (no eval, no process, AOT-safe): a sibling of `UpstreamPerlSubstitution` for this split/sort/map/join form is acceptable. Perl's `sort` is ordinal (byte) order.

## Acceptance criteria

- [ ] The program is interpreted exactly as Perl would on matching and non-matching lines, pinned by unit tests in `Curl.Conformance.UnitTests`.
- [ ] test1286 is no longer screened out; added to `PassingUpstreamCases.txt` if it passes, or its failure named in Notes with a follow-up task.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; build and fast tests green; `Curl.Conformance.UnitLibrary/CLAUDE.md` updated.

## Notes

## Log

- 2026-10-14: Created (split from BL-2223).
