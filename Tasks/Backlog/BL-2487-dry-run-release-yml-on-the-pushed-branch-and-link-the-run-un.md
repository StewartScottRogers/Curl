---
id: BL-2487
title: Dry-run release.yml on the pushed branch and link the run under BL-2485
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2485]
touches: [.github/workflows/release.yml]
lane: no
requirement: none
created: 2026-10-16
completed:
---
# BL-2487 — Dry-run release.yml on the pushed branch and link the run under BL-2485

## Goal

`release.yml` has been run by `workflow_dispatch` on the branch that carries BL-2485, and the 7 Curl.Mcp packages came out as artifacts.

## Context

BL-2485 added the `pack-mcp` job; a lane cannot push, so the dry run it asks for is left to an interactive session. If the Smoke test step fails because `curl --version` does not exit 0, restore the old allowance of 0 or 2.

## Acceptance criteria

- [ ] A `workflow_dispatch` run of `release.yml` succeeds, its `curl-mcp-packages` artifact holds 7 `.nupkg` files, and its link is under BL-2485's Notes.

## Notes

## Log

- 2026-10-16: Created.
