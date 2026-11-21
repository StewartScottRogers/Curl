---
id: BL-5709
title: Rename the NuGet package check in ci.yml's mcp-pack job to Surly.Curl.Mcp
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml]
lane: no
requirement: none
created: 2026-11-21
completed:
---
# BL-5709 — Rename the NuGet package check in ci.yml's mcp-pack job to Surly.Curl.Mcp

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-11-21: Created.

Notes: ci.yml is a guard file a lane cannot change (BL-5707 renamed the packages). In the `mcp-pack` job's "Check the packages" step change `'^Curl\.Mcp\.\d'` to `'^Surly\.Curl\.Mcp\.\d'` and `"Curl.Mcp.$env:RID.*"` to `"Surly.Curl.Mcp.$env:RID.*"` (and the error text). Interactive only: no lane may edit it.
- 2026-11-21: Backlog -> Doing. Criteria at claim: fbecace0.
