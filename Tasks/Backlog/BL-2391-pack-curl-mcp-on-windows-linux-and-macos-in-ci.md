---
id: BL-2391
title: Pack Curl.Mcp on Windows, Linux and macOS in CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2321, BL-2388, BL-2389, BL-2390]
touches: [.github/workflows/ci.yml]
lane: no
requirement: none
created: 2026-10-15
completed:
---

## Goal

Add a CI job that runs dotnet pack Curl.Mcp and the Curl.Mcp.IntegrationTests on all three platforms, uploading the .nupkg as an artifact.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

`.github/workflows/ci.yml` is a guard file (ADR-0267): this task is interactive only (`lane: no`) and goes through the `audit` branch route (root `CLAUDE.md`, "Audit office"), never a dark factory lane. Read the existing jobs and match their setup. The pack job does not publish.

## Acceptance criteria

- [ ] `ci.yml` has a job (or matrix entries) that packs `Curl.Mcp` on ubuntu, windows and macos and uploads the package, named so the log says which platform failed.
- [ ] `Curl.Mcp.IntegrationTests` run on all three platforms, or Notes states exactly which platform cannot and why.
- [ ] The first CI run on the merged branch is green on all three platforms; the run id is in Notes.
- [ ] The `audit-guard` job passes for the change (the PR goes through the audit route).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
