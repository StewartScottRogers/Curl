---
id: BL-2393
title: Publish Curl.Mcp to nuget.org as Surly.Curl.Mcp
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-2321, BL-2391, BL-2392]
touches: []
lane: no
requirement: none
created: 2026-10-15
completed: 2026-11-27
---

## Goal

Push the Curl.Mcp package, package ID `Surly.Curl.Mcp`, to nuget.org so `dnx Surly.Curl.Mcp` works and nuget.org lists it under packagetype=mcpserver.

## Context

ADR-0548 decision 7: Stewart approved publishing. This is a release step, not lane work. Since 2026-11-26 it works as Surl's does (ADR-1443). On every `v*` tag, `release.yml`'s release job logs in to nuget.org with `NuGet/login@v1` (user `StewartRogers86`, Stewart's nuget.org profile and the policy's creator; a one-hour key from GitHub's OIDC token) and pushes the tag's nine CI-built packages. No long-lived key exists. Stewart's one-time setup on nuget.org: the `Surly.` prefix reservation, and a Trusted Publishing policy for owner `StewartScottRogers`, repository `Curl`, workflow `release.yml`.

## Acceptance criteria

- [x] The package version, `.mcp/server.json` version and release notes agree; the packages are the ones `release.yml` built for the tag.
- [x] Stewart has reserved the `Surly.` prefix and created the Trusted Publishing policy on nuget.org.
- [x] A tag's release job pushed all nine packages; nuget.org shows `Surly.Curl.Mcp` with PackageType McpServer, and `Surly.Curl.Api` and `Surly.Libcurl` at the same version.
- [x] In a clean temp directory, `dnx Surly.Curl.Mcp --prerelease` starts and answers `initialize`; the result is in Notes.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- First nuget.org release: `v0.4.0-preview.5` (master 6657f3a91a), release run 46024336774. Its release job logged in as StewartRogers86, and nuget.org answered "Your package was pushed" for all nine packages. All nine were indexed at `0.4.0-preview.5` within about 20 minutes.
- `Surly.Curl.Mcp`'s nuspec on nuget.org has package types DotnetTool and McpServer, and `<license type="file">LICENSE.txt</license>`.
- `dnx Surly.Curl.Mcp --prerelease --yes` in an empty temp directory, sent `initialize`, answered (on the second try, after nuget.org's install feed caught up): `{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18",...,"serverInfo":{"name":"Curl.Mcp","version":"0.4.0.0"}}}`.
- What it took:
  - `v0.4.0-preview.3`: four login failures, HTTP 401. The job logged in as the GitHub name, not StewartRogers86. Fixed in 36f6a43027.
  - `v0.4.0-preview.4`: HTTP 400, because nuget.org refuses the license expression `curl`. The packages now embed LICENSE.txt (f8cfce5cea).
  - A rerun of a tag's job now keeps the existing GitHub release (6c83fc4089).
  - Neither preview.3 nor preview.4 is on nuget.org.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Deferred. Stewart 2026-10-16: no nuget.org push until the entire port is complete; look again when the backlog's port work is done
- 2026-11-18: Deferred -> Backlog. Stewart 2026-11-18: clear Deferred; returned to Backlog
- 2026-11-25: Package ID brought up to Stewart's 2026-11-21 choice of the `Surly.` prefix (`Surly.Curl.Mcp`); still held until the port is complete.
- 2026-11-26: Switched to nuget.org Trusted Publishing (Stewart: "I want to use Trusted Publishing"; ADR-1443, workflow nuget-publish.yml); still held until the port is complete.
- 2026-11-26: Stewart: "look in the Surl repository and do it the same way", every tag included: release.yml pushes on each v* tag; the hold is lifted (ADR-1443).
- 2026-11-27: Assignee Stewart -> Claude: Stewart did his part (nuget.org policy, "publish it"), and Trusted Publishing needs no key from him.
- 2026-11-27: Backlog -> Doing. Criteria at claim: df7b8fe6 3335b5a1 294fd4e6 eb80b75a.
- 2026-11-27: Doing -> Done. Curl v0.4.0-preview.5 is on nuget.org (9 packages, Trusted Publishing); dnx Surly.Curl.Mcp starts and answers initialize.
