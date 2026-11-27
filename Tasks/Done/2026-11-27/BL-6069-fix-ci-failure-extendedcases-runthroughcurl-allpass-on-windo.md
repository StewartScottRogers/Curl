---
id: BL-6069
title: Fix CI failure ExtendedCases_RunThroughCurl_AllPass on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6069 — Fix CI failure ExtendedCases_RunThroughCurl_AllPass on Windows

## Goal

`ExtendedCases_RunThroughCurl_AllPass` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExtendedCases_RunThroughCurl_AllPass` failed on Windows in CI run 45939128920 (https://github.com/StewartScottRogers/Curl/actions/runs/45939128920). First failing commit: 4bf0fe70.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 45939128920 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ExtendedCases_RunThroughCurl_AllPass` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a stall of the busy Windows runner, not a Gopher defect. The only failure was `GOPHER/ext4005:
  the case did not finish within 30 seconds` (the harness's whole-case limit, not the runner's 20 second
  curl limit). Between the last green run (5477178e) and 4bf0fe70 only gRPC and DPoP production code
  changed, and Curl.Conformance.UnitTests took 3 m 6 s against 1 m 10 s to 1 m 23 s in three green runs.
  Reading `GopherServerConnection` showed no lost wake-up between a write and a pending read.
- Fix: listed upstream cases already get a second attempt for exactly this (BL-1859, BL-2093); our own
  cases did not. Both now share `RunAgainIfFailedAsync`, and the first failure is written to the test's
  diagnostics, so a case that really broke still fails. Limits left unchanged. No ADR: it extends the
  existing rule to cases that must also pass.
- CI half of the criterion: a lane cannot see CI for its own push; the change is test-only and
  platform-neutral, and the build and fast tests are green on Windows. The shift's CI watch covers the rest.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 35574f54.
- 2026-11-26: Doing -> Done. A failing Extended case now gets a second attempt, as a listed upstream case does, so a CI runner stall no longer fails ExtendedCases_RunThroughCurl_AllPass.
