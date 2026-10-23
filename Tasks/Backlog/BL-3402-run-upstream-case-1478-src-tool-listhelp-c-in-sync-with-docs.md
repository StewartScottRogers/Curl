---
id: BL-3402
title: Run upstream case 1478 (src/tool_listhelp.c in sync with docs/cmdline-opts) by giving the managen interpretation a listhelp mode
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs, Curl.Conformance.UnitLibrary/UpstreamManagenScript.cs, Curl.Conformance.UnitTests/UpstreamManagenScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/ADR-0747-upstream-case-1478-stays-off-the-ratchet.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3402 — Run upstream case 1478 (src/tool_listhelp.c in sync with docs/cmdline-opts) by giving the managen interpretation a listhelp mode

## Goal

Upstream case 1478 (`scripts/managen ... listhelp docs/cmdline-opts/*.md`, whose output must equal upstream's `src/tool_listhelp.c`) runs through the C# `managen` interpretation with a `listhelp` mode and passes, and is on the ratchet.

## Context

Stewart (2026-10-23) wants every test passing. ADR-0747 (BL-2850) kept 1478 off the ratchet because the harness's `managen` interpretation (`UpstreamManagenScript`, registered in `UpstreamPerlCommandScripts.Managen.cs`) carries out only `mainpage` and `ascii`, not `listhelp`, so the case runs and its stdout differs. ADR-0747 rejected printing the C source from Curl's help table; the alternative here is to implement `listhelp` in `UpstreamManagenScript` as upstream's `managen` does (https://github.com/curl/curl/blob/curl-8_22_0/scripts/managen): read each `docs/cmdline-opts/*.md` header (`Short`, `Long`, `Arg`, `Help`, `Category`, `Protocols`), and print the licence header, `#include` lines and the `helptext[]` array exactly. The case's expected output is the vendored `UpstreamTestData/src/tool_listhelp.c`. This completes the managen interpretation (the deferred BL-2881 builds the real generator) and is a faithful check that the docs and upstream's committed C file agree; `CurlHelpTextTests` in `Curl.Cli.UnitTests` still guards Curl's own `--help`.

## Acceptance criteria

- [ ] `UpstreamManagenScript` handles the `listhelp` argument and prints bytes equal to the vendored `src/tool_listhelp.c` for the vendored `docs/cmdline-opts/*.md`, with focused tests in `UpstreamManagenScriptTests` (one row per header field kind and one for the sort order), and 100% line and branch coverage kept on `Curl.Conformance.UnitLibrary`.
- [ ] Case 1478 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1478'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] ADR-0747 gets a "Superseded by" note naming this task's decision and the reason (the interpretation can reproduce `listhelp` from the vendored docs), and its Consequences no longer say 1478 is excluded.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
