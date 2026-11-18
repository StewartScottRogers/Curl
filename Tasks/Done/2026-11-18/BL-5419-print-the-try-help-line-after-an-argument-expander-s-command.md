---
id: BL-5419
title: Print the try-help line after an argument expander's command-line refusal such as an unreadable --http-file or --openapi
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5402]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5419 — Print the try-help line after an argument expander's command-line refusal such as an unreadable --http-file or --openapi

## Goal

An argument expander's refusal (unreadable --http-file or --openapi) prints the try-help line after its message, as curl does for command-line errors.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

Decision: every ArgumentExpansionException gets the try-help line (all are command-line refusals). Quality run for Curl.Console: 0 failing members.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-17: Doing -> Done. Try-help line follows an argument expander refusal
