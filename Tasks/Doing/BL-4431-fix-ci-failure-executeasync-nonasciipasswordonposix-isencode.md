---
id: BL-4431
title: Fix CI failure ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousHttp.UnitTests, Curl.Extension.ObliviousHttp.UnitLibrary]
requirement: none
created: 2026-10-31
completed:
---
# BL-4431 — Fix CI failure ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8 on Linux and macOS

## Goal

`ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8` failed on Linux and macOS in CI run 41243817728 (https://github.com/StewartScottRogers/Curl/actions/runs/41243817728). First failing commit: edc61f80.

    CollectionAssert.AreEqual failed. Element at index 26 do not match.

Lanes test only on Windows, so reproduce with `gh run view 41243817728 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_NonAsciiPasswordOnPosix_IsEncodedAsUtf8` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: d3928e4f.
