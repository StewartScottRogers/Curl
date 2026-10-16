---
id: BL-2393
title: Publish Curl.Mcp to nuget.org
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

Push the Curl.Mcp package to nuget.org so dnx Curl.Mcp works and nuget.org lists it under packagetype=mcpserver.

## Context

ADR-0548 decision 7: Stewart approved publishing; the nuget.org API key is his alone. This is a release step, not lane work. Stewart supplies the key for one push (an environment variable in the session, never written to a file or the repository, never committed or logged); an interactive Claude session then runs the build and `dotnet nuget push`.

## Acceptance criteria

- [ ] The package version, `.mcp/server.json` version and release notes agree; the package was produced by CI's pack job from the commit being released.
- [ ] Stewart has supplied the API key for this push.
- [ ] `dotnet nuget push <Curl.Mcp.nupkg> --source https://api.nuget.org/v3/index.json` succeeded; nuget.org shows the package with PackageType McpServer.
- [ ] In a clean temp directory, `dnx Curl.Mcp` starts and answers `initialize`; the result is in Notes.
- [ ] The key appears in no file, log or saved command history in the repository.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Deferred. Stewart 2026-10-16: no nuget.org push until the entire port is complete; look again when the backlog's port work is done
