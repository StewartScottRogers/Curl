---
id: BL-4880
title: Fix CI failure AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-4880 — Fix CI failure AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket on Windows

## Goal

`AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket` failed on Windows in CI run 42146707789 (https://github.com/StewartScottRogers/Curl/actions/runs/42146707789). First failing commit: 8a883455.

    Assertion failed. Expected value to not be null.

Lanes test only on Windows, so reproduce with `gh run view 42146707789 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: eff750ba.
