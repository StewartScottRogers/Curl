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
completed: 2026-10-16
---
# BL-2487 — Dry-run release.yml on the pushed branch and link the run under BL-2485

## Goal

`release.yml` has been run by `workflow_dispatch` on the branch that carries BL-2485, and the 7 Curl.Mcp packages came out as artifacts.

## Context

BL-2485 added the `pack-mcp` job; a lane cannot push, so the dry run it asks for is left to an interactive session. If the Smoke test step fails because `curl --version` does not exit 0, restore the old allowance of 0 or 2.

## Acceptance criteria

- [x] A `workflow_dispatch` run of `release.yml` succeeds, its `curl-mcp-packages` artifact holds 7 `.nupkg` files, and its link is under BL-2485's Notes.

## Notes

- Run https://github.com/StewartScottRogers/Curl/actions/runs/38931416963 on `work/dark-factory` 5878d24f2: success. Pack Curl.Mcp and all six Publish jobs passed (the smoke test's `curl --version` exits 0 on every platform); Create the GitHub release skipped, as no tag.
- `curl-mcp-packages` holds 7 packages, all `0.0.0-dev.115`: `Curl.Mcp` plus `Curl.Mcp.<rid>` for win, linux and osx, x64 and arm64.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. release.yml dry run 38931416963 green: six curl packages and seven Curl.Mcp packages build; linked under BL-2485
