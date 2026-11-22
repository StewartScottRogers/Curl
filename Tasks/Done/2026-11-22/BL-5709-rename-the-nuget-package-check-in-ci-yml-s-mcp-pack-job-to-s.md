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
completed: 2026-11-21
---
# BL-5709 — Rename the NuGet package check in ci.yml's mcp-pack job to Surly.Curl.Mcp

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- 2026-11-21 (interactive session): this task was filed with the template's placeholder as its only criterion; the work it names (rename the package check in ci.yml's mcp-pack job) is done as below. Fixed by `.github/workflows/ci.yml` (guard file, audit branch `e0dca8dce9`, PR #412 merged to master as `18c1d39a1e`, into work/dark-factory as `066365e9de`): the `Pack Curl.Mcp` job's `Check the packages` step now reads `<PackageId>` from `Curl.Mcp/Curl.Mcp.csproj` instead of matching the literal `Curl.Mcp` names, so the BL-5707 rename to `Surly.Curl.Mcp` passes. CI: PR #412 green on Windows, Linux and macOS (head `e0dca8dce9`), and the `CI` workflow on work/dark-factory at `066365e9de` (run 44961136795) succeeded, `Pack Curl.Mcp` passing on all three. Locally (Windows): `dotnet pack Curl.Mcp` produced `Surly.Curl.Mcp.0.1.0.nupkg`, which the step's tool-package check accepts and which holds `.mcp/server.json`; the per-RID native-AOT pack part of the step ran on each CI runner.

## Log

- 2026-11-21: Created.

Notes: ci.yml is a guard file a lane cannot change (BL-5707 renamed the packages). In the `mcp-pack` job's "Check the packages" step change `'^Curl\.Mcp\.\d'` to `'^Surly\.Curl\.Mcp\.\d'` and `"Curl.Mcp.$env:RID.*"` to `"Surly.Curl.Mcp.$env:RID.*"` (and the error text). Interactive only: no lane may edit it.
- 2026-11-21: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-21: Doing -> Done. The Pack Curl.Mcp check reads the package ID from the project; CI green on all three platforms after the Surly. rename
