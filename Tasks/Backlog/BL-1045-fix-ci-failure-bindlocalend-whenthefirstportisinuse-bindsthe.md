---
id: BL-1045
title: Fix CI failure BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-09-30
completed:
---
# BL-1045 — Fix CI failure BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext on Windows

## Goal

`BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` failed on Windows in CI run 36761924824 (https://github.com/StewartScottRogers/Curl/actions/runs/36761924824). First failing commit: 9691137a.

    Test method Curl.Networking.TcpDialerTests.BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext threw exception:

Lanes test only on Windows, so reproduce with `gh run view 36761924824 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-09-30: Created.
