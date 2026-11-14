---
id: BL-5158
title: Fix CI failure RunAsync_H3EntryForAnotherPort_ConnectsThereOverHttp3AloneWithAltUsed on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5158 — Fix CI failure RunAsync_H3EntryForAnotherPort_ConnectsThereOverHttp3AloneWithAltUsed on Linux and macOS

## Goal

`RunAsync_H3EntryForAnotherPort_ConnectsThereOverHttp3AloneWithAltUsed` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_H3EntryForAnotherPort_ConnectsThereOverHttp3AloneWithAltUsed` failed on Linux and macOS in CI run 43527663292 (https://github.com/StewartScottRogers/Curl/actions/runs/43527663292). First failing commit: 73f075a3.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43527663292 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_H3EntryForAnotherPort_ConnectsThereOverHttp3AloneWithAltUsed` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: BL-5154 changed the test to `--http3-only`, which looks entries up under `h3` only, so the `h1` entry for port 18444 never matched and the route was null (it also failed on Windows once the OSCondition was lifted). Fix: the test uses `--http3` (looks up h3, h2, h1; an h1 entry naming h3 switches to HTTP/3 alone) and drops the Windows exclusion. Test-only change; CI on Linux/macOS met by being platform-neutral, fast tests green here.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: d9c1ab0a.
- 2026-11-13: Doing -> Done. Test used --http3-only so the h1 entry never matched; now uses --http3
