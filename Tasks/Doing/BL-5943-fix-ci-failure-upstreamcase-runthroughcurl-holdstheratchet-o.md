---
id: BL-5943
title: Fix CI failure UpstreamCase_RunThroughCurl_HoldsTheRatchet on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-11-25
completed:
---
# BL-5943 — Fix CI failure UpstreamCase_RunThroughCurl_HoldsTheRatchet on Windows

## Goal

`UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `UpstreamCase_RunThroughCurl_HoldsTheRatchet` failed on Windows in CI run 45585553449 (https://github.com/StewartScottRogers/Curl/actions/runs/45585553449). First failing commit: de0db64e.

    Assertion failed.

Failing data rows: `test2407 [http/2]: checking curl_multi_waitfds functionality (HTTP/2)`.

Lanes test only on Windows, so reproduce with `gh run view 45585553449 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: bf51ea8b.
