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

ADR-0548 decision 7: Stewart approved publishing. This is a release step, not lane work. Since 2026-11-26 it works as Surl's does (ADR-1443). On every `v*` tag, `release.yml`'s release job logs in to nuget.org with `NuGet/login@v1` (user `StewartScottRogers`, a one-hour key from GitHub's OIDC token) and pushes the tag's nine CI-built packages. No long-lived key exists. Stewart's one-time setup on nuget.org: the `Surly.` prefix reservation, and a Trusted Publishing policy for owner `StewartScottRogers`, repository `Curl`, workflow `release.yml`.

## Acceptance criteria

- [ ] The package version, `.mcp/server.json` version and release notes agree; the packages are the ones `release.yml` built for the tag.
- [ ] Stewart has reserved the `Surly.` prefix and created the Trusted Publishing policy on nuget.org.
- [ ] A tag's release job pushed all nine packages; nuget.org shows `Surly.Curl.Mcp` with PackageType McpServer, and `Surly.Curl.Api` and `Surly.Libcurl` at the same version.
- [ ] In a clean temp directory, `dnx Surly.Curl.Mcp --prerelease` starts and answers `initialize`; the result is in Notes.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Deferred. Stewart 2026-10-16: no nuget.org push until the entire port is complete; look again when the backlog's port work is done
- 2026-11-18: Deferred -> Backlog. Stewart 2026-11-18: clear Deferred; returned to Backlog
- 2026-11-25: Package ID brought up to Stewart's 2026-11-21 choice of the `Surly.` prefix (`Surly.Curl.Mcp`); still held until the port is complete.
- 2026-11-26: Switched to nuget.org Trusted Publishing (Stewart: "I want to use Trusted Publishing"; ADR-1443, workflow nuget-publish.yml); still held until the port is complete.
- 2026-11-26: Stewart: "look in the Surl repository and do it the same way", every tag included: release.yml pushes on each v* tag; the hold is lifted (ADR-1443).
