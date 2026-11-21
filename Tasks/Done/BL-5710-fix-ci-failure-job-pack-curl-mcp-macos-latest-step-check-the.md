---
id: BL-5710
title: Fix CI failure job Pack Curl.Mcp (macos-latest) step Check the packages on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [.github/workflows/ci.yml]
lane: no
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5710 — Fix CI failure job Pack Curl.Mcp (macos-latest) step Check the packages on macOS

## Goal

`job Pack Curl.Mcp (macos-latest) step Check the packages` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (macos-latest) step Check the packages` failed on macOS in CI run 44941565490 (https://github.com/StewartScottRogers/Curl/actions/runs/44941565490). First failing commit: c3c60a47.

    ##[error]Process completed with exit code 1.

Lanes test only on Windows, so reproduce with `gh run view 44941565490 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Pack Curl.Mcp (macos-latest) step Check the packages` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Interactive only: the cause is in `.github/workflows/ci.yml` (guard file, lanes may not change it). BL-5707 prefixed the package IDs with Surly., so the "Check the packages" step fails on every OS: line 265 expects `^Curl.Mcp.d` and line 272 expects `Curl.Mcp.$env:RID.*`; the packages are now `Surly.Curl.Mcp.<rid>.*` (log: `Surly.Curl.Mcp.win-x64.0.1.0.nupkg`). Fix: change both patterns to `^Surly.Curl.Mcp.d` and `Surly.Curl.Mcp.$env:RID.*`, and the message text. BL-5711 is the same failure on ubuntu. Remove `lane: no` once ci.yml is fixed (or just close this task then).

- 2026-11-21 (interactive session): Fixed by `.github/workflows/ci.yml` (guard file, audit branch `e0dca8dce9`, PR #412 merged to master as `18c1d39a1e`, into work/dark-factory as `066365e9de`): the `Pack Curl.Mcp` job's `Check the packages` step now reads `<PackageId>` from `Curl.Mcp/Curl.Mcp.csproj` instead of matching the literal `Curl.Mcp` names, so the BL-5707 rename to `Surly.Curl.Mcp` passes. CI: PR #412 green on Windows, Linux and macOS (head `e0dca8dce9`), and the `CI` workflow on work/dark-factory at `066365e9de` (run 44961136795) succeeded, `Pack Curl.Mcp` passing on all three. Locally (Windows): `dotnet pack Curl.Mcp` produced `Surly.Curl.Mcp.0.1.0.nupkg`, which the step's tool-package check accepts and which holds `.mcp/server.json`; the per-RID native-AOT pack part of the step ran on each CI runner.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4090b3a3.
- 2026-11-21: Doing -> Backlog. Interactive only: fix is in guard file ci.yml (Surly. package prefix); lane: no
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4090b3a3.
- 2026-11-21: Doing -> Done. The Pack Curl.Mcp check reads the package ID from the project; CI green on all three platforms after the Surly. rename
