---
id: BL-3096
title: Confirm CI is green on Linux and macOS for the commit that holds ports 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3095]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-22
completed:
---
# BL-3096 — Confirm CI is green on Linux and macOS for the commit that holds ports 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304

## Goal

CI is green on Linux and macOS for the commit that holds the ten ported cases.

## Context

Split from BL-2847 (over its cost cap). All ten cases are already ported and on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303, 3304); BL-3062 ported 1650. What is left is confirming, with no new code expected.

## Acceptance criteria

- [ ] After the push, `CI` is green on Linux and macOS for the commit that holds the ten ports (`gh run list` names the run and its result).

## Notes

Split from BL-2847. Needs BL-3095 done first.

## Log

- 2026-10-22: Created.
