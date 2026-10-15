---
id: BL-2351
title: Fix CI failure 1024 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Tftp.UnitLibrary]
requirement: none
created: 2026-10-15
completed:
---
# BL-2351 — Fix CI failure 1024 on Windows

## Goal

`1024` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `1024` failed on Windows in CI run 38796134816 (https://github.com/StewartScottRogers/Curl/actions/runs/38796134816). First failing commit: 58485c6f.

    CollectionAssert.AreEqual failed. Element at index 46 do not match.

Lanes test only on Windows, so reproduce with `gh run view 38796134816 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `1024` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-15: Created.
