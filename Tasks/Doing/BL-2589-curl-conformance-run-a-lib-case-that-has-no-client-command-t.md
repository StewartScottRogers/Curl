---
id: BL-2589
title: Curl.Conformance: run a lib case that has no client command (test501, test509)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2589 — Curl.Conformance: run a lib case that has no client command (test501, test509)

## Goal

An upstream lib case with no `<client><command>` (test501, test509) runs its ported program with no arguments instead of being skipped.

## Context

Found by BL-2527: `lib501` and `lib509` are ported and registered in `LibTestPrograms.Ported`, but `CURL_UPSTREAM_CASE=501` and `509` answer "the case has no <client><command>". runtests.pl runs a tool with an empty command; `LibTestContext` needs at least a URL argument, so the runner should pass what runtests.pl passes (an empty argument list means `URL` is NULL upstream; decide how `LibTestContext` carries that, ADR if needed). test501 expects exit 3, test509 expects `Callbacks were invoked!` (which also needs Libcurl to call the `curl_global_init_mem` callbacks; file that separately if still failing).

## Acceptance criteria

- [ ] test501 runs through `lib501` and passes, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] test509 runs through `lib509` instead of being skipped; if it then fails only because Libcurl never calls the memory callbacks, that is filed as its own task.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 803176a2 99aa93a6 904b57ec 3d836d5e.
