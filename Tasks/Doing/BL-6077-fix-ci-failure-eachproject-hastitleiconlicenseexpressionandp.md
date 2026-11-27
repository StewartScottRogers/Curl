---
id: BL-6077
title: Fix CI failure EachProject_HasTitleIconLicenseExpressionAndProjectUrl on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests, Curl.Api.UnitLibrary, Curl.Api]
requirement: none
created: 2026-11-27
completed:
---
# BL-6077 — Fix CI failure EachProject_HasTitleIconLicenseExpressionAndProjectUrl on Linux and macOS

## Goal

`EachProject_HasTitleIconLicenseExpressionAndProjectUrl` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EachProject_HasTitleIconLicenseExpressionAndProjectUrl` failed on Linux and macOS in CI run 46017114393 (https://github.com/StewartScottRogers/Curl/actions/runs/46017114393). First failing commit: f8cfce5c.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 46017114393 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `EachProject_HasTitleIconLicenseExpressionAndProjectUrl` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 63a983c0.
