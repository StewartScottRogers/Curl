---
id: BL-1651
title: Fix CI failure EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitTests, Curl.Cookies.UnitLibrary]
requirement: none
created: 2026-10-07
completed:
---
# BL-1651 — Fix CI failure EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother on Linux and macOS

## Goal

`EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` failed on Linux and macOS in CI run 37694677072 (https://github.com/StewartScottRogers/Curl/actions/runs/37694677072). First failing commit: 5c771af7.

    CollectionAssert.AreEqual failed. Element at index 40 do not match.

Lanes test only on Windows, so reproduce with `gh run view 37694677072 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-07: Created.
