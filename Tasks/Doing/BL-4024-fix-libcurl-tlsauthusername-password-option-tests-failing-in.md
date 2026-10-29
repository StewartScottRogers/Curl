---
id: BL-4024
title: Fix Libcurl TlsauthUsername/Password option tests failing intermittently with NotBuiltIn under Measure-CodeQuality
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl.UnitTests/Options/Authentication/EasyAuthenticationOptionSettersTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4024 — Fix Libcurl TlsauthUsername/Password option tests failing intermittently with NotBuiltIn under Measure-CodeQuality

## Goal

The TlsauthUsername and TlsauthPassword option tests stop failing intermittently with NotBuiltIn.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] The Libcurl.UnitTests fast tests pass (1504 passed): BL-4020 made the SrpIsBuiltIn-flipping classes [DoNotParallelize], which was the cause.

## Notes

Already fixed by BL-4020 (3f3c6209a, DoNotParallelize on the classes that flip SrpIsBuiltIn), as with BL-4023. Fast tests green here.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
