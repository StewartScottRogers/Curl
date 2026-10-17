---
id: BL-2589
title: Curl.Conformance: run a lib case that has no client command (test501, test509)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2589 — Curl.Conformance: run a lib case that has no client command (test501, test509)

## Goal

An upstream lib case with no `<client><command>` (test501, test509) runs its ported program with no arguments instead of being skipped.

## Context

Found by BL-2527: `lib501` and `lib509` are ported and registered in `LibTestPrograms.Ported`, but `CURL_UPSTREAM_CASE=501` and `509` answer "the case has no <client><command>". runtests.pl runs a tool with an empty command; `LibTestContext` needs at least a URL argument, so the runner should pass what runtests.pl passes (an empty argument list means `URL` is NULL upstream; decide how `LibTestContext` carries that, ADR if needed). test501 expects exit 3, test509 expects `Callbacks were invoked!` (which also needs Libcurl to call the `curl_global_init_mem` callbacks; file that separately if still failing).

## Acceptance criteria

- [x] test501 runs through `lib501` and passes, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] test509 runs through `lib509` instead of being skipped; if it then fails only because Libcurl never calls the memory callbacks, that is filed as its own task.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitLibrary` and `.UnitTests` to `touches`: `LibTestContext` refused an empty argument list, and no other task in Doing on `origin/work/dark-factory` named them.
- Decision (small enough for Notes, no ADR): `LibTestContext` now accepts no arguments and `Url` is `string?`, `null` then, as libtest's `first.c` leaves `URL` = `argv[1]` = NULL when runtests.pl runs a tool with an empty command. `Lib506` passes `context.Url!` to `SubUrl`, which still throws on null (test506 always has a URL).
- Screening: a case with no `<client><command>` is no longer skipped when its `<tool>` names a libtest program; the runner already gives such a program the command's words, none here.
- test501 passes (exit 3) and is on the ratchet; it depends on no platform (no output, no paths). test509 now runs and fails only because Libcurl never calls the `curl_global_init_mem` callbacks (stdout empty): filed as BL-2598.
- Fast tests green (Curl.Conformance.UnitTests 3171 passed, LibTest 206 passed); Measure-CodeQuality for both libraries: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 803176a2 99aa93a6 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Done. A lib case with no <client><command> runs its program with no arguments: test501 passes and is on the ratchet, test509 runs (BL-2598 owns its memory callbacks)
