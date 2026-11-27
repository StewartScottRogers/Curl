---
id: BL-2393
title: Publish Curl.Mcp to nuget.org as Surly.Curl.Mcp
priority: Low
assignee: Stewart
pipeline: direct
depends-on: [BL-2321, BL-2391, BL-2392]
touches: []
lane: no
requirement: none
created: 2026-10-15
completed:
---

## Goal

Push the Curl.Mcp package, package ID `Surly.Curl.Mcp`, to nuget.org so `dnx Surly.Curl.Mcp` works and nuget.org lists it under packagetype=mcpserver.

## Context

ADR-0548 decision 7: Stewart approved publishing. This is a release step, not lane work. Since 2026-11-26 it uses nuget.org Trusted Publishing (ADR-1443). `.github/workflows/nuget-publish.yml` is run by hand with a numbered tag, waits for Stewart's approval in the `nuget` environment, and pushes the release's CI-built packages with a one-hour key from `NuGet/login`. No long-lived key exists. Stewart's one-time setup: the `Surly.` prefix reservation and a trust policy on nuget.org (owner `StewartScottRogers`, repository `Curl`, workflow `nuget-publish.yml`, environment `nuget`), the `nuget` environment in GitHub with himself as required reviewer, and the repository variable `NUGET_USER` (his nuget.org profile name).

## Acceptance criteria

- [ ] The package version, `.mcp/server.json` version and release notes agree; the packages are the ones `release.yml` built for the numbered tag being published.
- [ ] Stewart has reserved the `Surly.` prefix and created the trust policy on nuget.org, and the `nuget` environment and `NUGET_USER` variable exist in GitHub.
- [ ] `gh workflow run nuget-publish.yml -f tag=<tag>`, approved by Stewart, succeeded; nuget.org shows `Surly.Curl.Mcp` with PackageType McpServer, and `Surly.Curl.Api` and `Surly.Libcurl` at the same version.
- [ ] In a clean temp directory, `dnx Surly.Curl.Mcp --prerelease` starts and answers `initialize`; the result is in Notes.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Deferred. Stewart 2026-10-16: no nuget.org push until the entire port is complete; look again when the backlog's port work is done
- 2026-11-18: Deferred -> Backlog. Stewart 2026-11-18: clear Deferred; returned to Backlog
- 2026-11-25: Package ID brought up to Stewart's 2026-11-21 choice of the `Surly.` prefix (`Surly.Curl.Mcp`); still held until the port is complete.
- 2026-11-26: Switched to nuget.org Trusted Publishing (Stewart: "I want to use Trusted Publishing"; ADR-1443, workflow nuget-publish.yml); still held until the port is complete.
