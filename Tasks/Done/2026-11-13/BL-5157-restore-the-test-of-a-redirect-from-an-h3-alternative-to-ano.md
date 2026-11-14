---
id: BL-5157
title: Restore the test of a redirect from an h3 alternative to another origin over TCP
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerAltSvcVersionTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5157 — Restore the test of a redirect from an h3 alternative to another origin over TCP

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

Restored RunAsync_RedirectFromAnH3AlternativeToAnotherOrigin_ConnectsToItOverTcp with --http3 and a FirstQuicOnlyConnector (the redirect target's own QUIC attempt is refused and falls back to TCP). Fast tests of the class pass.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-13: Doing -> Done. Redirect-from-h3 test restored with --http3
