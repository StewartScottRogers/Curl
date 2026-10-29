---
id: BL-4268
title: Fix CI failure ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4268 — Fix CI failure ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails on Linux

## Goal

`ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails` failed on Linux in CI run 40957132484 (https://github.com/StewartScottRogers/Curl/actions/runs/40957132484). First failing commit: b03266d4.

    Assertion failed. Expected condition to be false.

Lanes test only on Windows, so reproduce with `gh run view 40957132484 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_SockoptReturnsAlreadyConnectedWithoutOpenSocket_ConnectsNothingSoTheFirstWriteFails` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 395bec4e.
