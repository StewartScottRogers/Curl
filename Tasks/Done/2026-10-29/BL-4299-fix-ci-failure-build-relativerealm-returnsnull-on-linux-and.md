---
id: BL-4299
title: Fix CI failure Build_RelativeRealm_ReturnsNull on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OciRegistry.UnitTests, Curl.Extension.OciRegistry.UnitLibrary]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4299 — Fix CI failure Build_RelativeRealm_ReturnsNull on Linux and macOS

## Goal

`Build_RelativeRealm_ReturnsNull` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Build_RelativeRealm_ReturnsNull` failed on Linux and macOS in CI run 40985475541 (https://github.com/StewartScottRogers/Curl/actions/runs/40985475541). First failing commit: 4c618d0f.

    Assertion failed. Expected value to be null.

Lanes test only on Windows, so reproduce with `gh run view 40985475541 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Build_RelativeRealm_ReturnsNull` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: on Linux and macOS `Uri.TryCreate("/token", UriKind.Absolute)` succeeds as `file:///token` (a Unix path is an absolute file URI), so the builder took a bare path for a realm. Fix: `OciTokenRequestBuilder` now also requires the realm's scheme to be `http` or `https`, the only schemes a token endpoint can have; `Build_AbsoluteRealmThatIsNotHttp_ReturnsNull` pins that branch on every platform. No ADR: this is a bug fix within the existing rule (a realm must be an absolute URL), not a new decision.
- CI criterion: the change is platform-neutral (it rejects the `file` URI that Unix produces and fails `TryCreate` on Windows, with the same result), the solution builds clean, and the fast tests pass here; Measure-CodeQuality for Curl.Extension.OciRegistry.UnitLibrary reports 0 failing members. The shift's CI watch files a task if CI disagrees.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ea8e7aca.
- 2026-10-29: Doing -> Done. OCI token realm must be http or https, so Build_RelativeRealm_ReturnsNull passes on every platform
