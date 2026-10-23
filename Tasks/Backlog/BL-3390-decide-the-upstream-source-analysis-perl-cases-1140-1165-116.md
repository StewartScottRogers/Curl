---
id: BL-3390
title: Decide the upstream source-analysis perl cases 1140, 1165, 1169, 1173, 1182, 1185 and 1275 against the vendored upstream files, and supersede the no-counterpart rows of ADR-0718 and ADR-0755 for 745, 1135, 1167, 1276, 1279 and 1544
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/README.md, Documentation/Planning/Decisions/ADR-0718-upstream-c-source-and-man-page-tooling-cases-have-no-counterpart-except-managen.md, Documentation/Planning/Decisions/ADR-0755-upstream-cases-745-and-1276-check-c-only-header-tooling.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3390 — Decide the upstream source-analysis perl cases 1140, 1165, 1169, 1173, 1182, 1185 and 1275 against the vendored upstream files, and supersede the no-counterpart rows of ADR-0718 and ADR-0755 for 745, 1135, 1167, 1276, 1279 and 1544

## Goal

Each of upstream cases 1140, 1165, 1169, 1173, 1182, 1185 and 1275 has a decision made against what the repository can now read (the vendored headers, docs and data under `Curl.Conformance.UnitTests/UpstreamTestData`), and the no-counterpart rows of ADR-0718 and ADR-0755 for cases 745, 1135, 1167, 1276, 1279 and 1544 are superseded by a decision to interpret their scripts.

## Context

Stewart (2026-10-23) wants every test active and passing on Windows. These cases all skip with `the harness does not run a perl command` (1182: `the harness does not interpret the <client><precheck> line mkdir ...`). ADR-0718 (BL-2852) and ADR-0755 (BL-2851) recorded 1135, 1140, 1165, 1167, 1169, 1173, 1185, 1275, 1279, 1544, 745 and 1276 as `no counterpart` because "Curl has no C headers". ADR-0749 (BL-2991) later ran 1119, 1175 and 1477 anyway, through C# interpretations that read the vendored upstream headers, on the reasoning that these cases check upstream's tree for consistency and the vendored files are that tree. ADR-0714 is the mechanism (a registered C# interpretation per script name, one `UpstreamPerlCommandScripts.Test<N>.cs` file each). The upstream scripts are in https://github.com/curl/curl/tree/curl-8_22_0/tests (`test1135.pl`, `test1140.pl`, `test1165.pl`, `test1167.pl`, `test1173.pl`, `test1275.pl`, `test1276.pl`, `test1544.pl`, `test745.pl`), plus `scripts/checksrc.pl` and `tests/runtests.pl`.

Already vendored: `include/curl/*.h` (including `typecheck-gcc.h`), `docs/libcurl/*.md`, `docs/libcurl/symbols-in-versions`, `docs/cmdline-opts/*.md`, three `src/tool_*.c` files. BL-3391 adds `lib/libcurl.def`, `lib/optiontable.pl`, `lib/easyoptions.c` and `packages/OS400/ccsidcurl.c`. Curl already has a cd2nroff (`UpstreamCd2nroffTests`, ADR-0750), which may let 1140 and 1173 run over generated `.3` pages the way 1222 does (see the BL-2992 Notes on 1222).

This task decides; it does not implement. Cases 745, 1135 and 1279 (`test1135.pl`), 1167, 1276 and 1544 are decided here as `interpret` and already have their own port tasks (filed 2026-10-23, depending on this task).

## Acceptance criteria

- [ ] One new ADR, "Decided by Claude under Stewart's delegation", has one row per case for 1140, 1165, 1169, 1173, 1182, 1185 and 1275: `interpret` (naming the vendored or to-be-vendored upstream files the script needs) or `no counterpart` with the exact C-only thing it tests. No row gives difficulty or a missing BCL primitive as its reason (root CLAUDE.md, "Decisions"). For 1169 and 1182, which run upstream's runtests.pl with `-L` and `-E`, the row says whether the C# runner can honour those options and the 1182 precheck line (`mkdir`, `cp`, `echo`).
- [ ] The same ADR states that ADR-0718's rows for 1135, 1167 and 1279 and ADR-0755's rows for 745, 1276 and 1544 are superseded by ADR-0749's reasoning, and names the port task for each (the tasks filed 2026-10-23 for those cases).
- [ ] Every case this ADR decides as `interpret` has a port task filed on the board (naming the case, depending on this task, touching only that script's `UpstreamTest<N>Script.cs`, `UpstreamPerlCommandScripts.Test<N>.cs`, `UpstreamTest<N>ScriptTests.cs` and the ratchet).
- [ ] ADR-0718 and ADR-0755 each get a one-line "Superseded in part by ADR-nnnn" status note, and the Decisions README.md index lists the new ADR.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
