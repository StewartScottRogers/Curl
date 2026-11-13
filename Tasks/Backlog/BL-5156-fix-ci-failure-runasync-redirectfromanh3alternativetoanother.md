---
id: BL-5156
title: Fix CI failure RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed:
---
# BL-5156 — Fix CI failure RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp on Linux and macOS

## Goal

`RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp` failed on Linux and macOS in CI run 43523918778 (https://github.com/StewartScottRogers/Curl/actions/runs/43523918778). First failing commit: 73f075a3.

    Assertion failed. Expected collection to contain a specific number of elements.

Lanes test only on Windows, so reproduce with `gh run view 43523918778 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-13: Created.
