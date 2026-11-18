---
id: BL-5417
title: Fix CI failure job Quality gates (windows-latest) step Every shard succeeded on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-17
completed:
---
# BL-5417 — Fix CI failure job Quality gates (windows-latest) step Every shard succeeded on Windows

## Goal

`job Quality gates (windows-latest) step Every shard succeeded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Every shard succeeded` failed on Windows in CI run 44224595726 (https://github.com/StewartScottRogers/Curl/actions/runs/44224595726). First failing commit: 10f2e1ba.

    ∩╗┐2026-11-18T02:47:54.3233313Z ##[group]Run if ('failure' -ne 'success') {

Lanes test only on Windows, so reproduce with `gh run view 44224595726 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Every shard succeeded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-17: Created.
