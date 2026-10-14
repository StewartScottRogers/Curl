---
id: BL-2175
title: Upstream harness gives %GOPHERPORT and %HTTP2PORT values (upstream test1609, test5028, test2407)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2175 — Upstream harness gives %GOPHERPORT and %HTTP2PORT values (upstream test1609, test5028, test2407)

## Goal

The upstream harness substitutes `%GOPHERPORT` and `%HTTP2PORT` and routes them to an in-memory server, so upstream test1609, test5028 (gopher) and test2407 (HTTP/2) run instead of skipping.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13: the three cases are skipped with "the harness has no value for %GOPHERPORT" / "%HTTP2PORT". test2407 is also a `<client><tool>` libtest, so it may still skip for that reason afterwards; say so under Notes. Harness: `Curl.Conformance.UnitLibrary` (ADR-0013, ADR-0460).

## Acceptance criteria

- [ ] test1609 and test5028 no longer skip for a missing port variable; each one that passes is on `PassingUpstreamCases.txt`, and each that fails has its cause filed as a task.
- [ ] `%HTTP2PORT` has a value, and test2407's remaining skip reason (if any) is recorded under Notes.
- [ ] Harness unit tests cover the new variables; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
