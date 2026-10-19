---
id: BL-2852
title: Record why upstream's C-source and man-page tooling cases have no counterpart in Curl: cases 1135, 1140, 1165, 1167, 1169, 1173, 1185, 1275, 1279, 1544, 1705 and 1706
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2852 — Record why upstream's C-source and man-page tooling cases have no counterpart in Curl: cases 1135, 1140, 1165, 1167, 1169, 1173, 1185, 1275, 1279, 1544, 1705 and 1706

## Goal

Each of upstream cases 1135, 1140, 1165, 1167, 1169, 1173, 1185, 1275, 1279, 1544, 1705 and 1706 has a recorded verdict: either it is covered by a port task (named) or the C-only upstream tooling it tests is named in one ADR, so none is left unexplained.

## Context

Gap scorecard 2026-10-19_1329 has these twelve `excluded | upstream-source-check`; measured 2026-10-19 on Windows they are skipped with `the harness does not run a perl command`. Their `<command type="perl">` runs a script in upstream curl 8.22.0's source tree (https://github.com/curl/curl/tree/curl-8_22_0) that checks the upstream repository itself: 1135 `CURL_EXTERN` order in the headers and 1279 `libcurl.def` against the `CURL_EXTERN` declarations (C export lists), 1140 and 1173 man page syntax (nroff), 1165 `configure.ac` against `CURL_DISABLE_` options, 1167 the `curl` prefix of public symbols in C headers, 1169 `runtests.pl -L` accepts a library option, 1185 `checksrc.pl` (the C style checker), 1275 capital letters after a period in the Markdown docs, 1544 the OS/400 wrapper translates every string option, 1705 and 1706 `managen` builds a man page and an ASCII page from `option1.md` and `option2.md`.

None tests a behaviour of the `curl` command line or of the libcurl API. Curl has no C headers, `configure.ac`, nroff pages, C style checker, OS/400 wrapper or `managen`. For each case read its script and decide, by BL-2840's rule, between (a) the invariant has a Curl counterpart - then say which task covers it (BL-2850 and BL-2851 cover the option and libcurl tables; file a new port task for any other) and (b) no counterpart: write the C-only thing the case tests. A reason is never "hard" or "the BCL has no primitive" (root CLAUDE.md, "Decisions"): if a case checks something a complete C# reimplementation should also have (for example `--manual` output built from option documents, 1705 and 1706), file the task that builds it instead of recording a refusal.

## Acceptance criteria

- [ ] One new ADR, "Decided by Claude under Stewart's delegation", has one row for each of the twelve cases: `covered by BL-####` or `no counterpart` plus the upstream-only thing it tests.
- [ ] Every row that says covered names a task that exists on the board; every case that needs a new task has that task filed (it names the case and depends on BL-2850 when it needs the perl-command mechanism).
- [ ] No row gives difficulty or a missing BCL primitive as its reason.
- [ ] `Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs` is not changed by this task; the skip reason `the harness does not run a perl command` stays for these cases and `PassingUpstreamCases.txt` is unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-19: Created.
