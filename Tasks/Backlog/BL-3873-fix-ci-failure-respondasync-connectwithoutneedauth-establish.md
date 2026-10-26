---
id: BL-3873
title: Fix CI failure RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitTests, Curl.Conformance.Differential.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3873 — Fix CI failure RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays on Linux

## Goal

`RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays` failed on Linux in CI run 40396898273 (https://github.com/StewartScottRogers/Curl/actions/runs/40396898273). First failing commit: 4a95abee.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40396898273 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
