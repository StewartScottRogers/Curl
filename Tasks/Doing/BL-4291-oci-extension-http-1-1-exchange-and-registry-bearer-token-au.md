---
id: BL-4291
title: OCI extension: HTTP/1.1 exchange and registry Bearer token auth
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OciRegistry.UnitLibrary/Curl.Extension.OciRegistry.UnitLibrary.csproj, Curl.Extension.OciRegistry.UnitLibrary/Http, Curl.Extension.OciRegistry.UnitLibrary/Auth, Curl.Extension.OciRegistry.UnitTests/Curl.Extension.OciRegistry.UnitTests.csproj, Curl.Extension.OciRegistry.UnitTests/Http, Curl.Extension.OciRegistry.UnitTests/Auth, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4291 — OCI extension: HTTP/1.1 exchange and registry Bearer token auth

## Goal

`Curl.Extension.OciRegistry.UnitLibrary` exists with an HTTP/1.1 request/response exchange over an `IConnection` and the registry token flow, so the pull (BL-4292) and push (BL-4293) flows can talk to a registry.

## Context

Split from BL-4127 (extension 26 of 63; ADR-0999, CLAUDE.md "Extensions"). Specification: OCI Distribution Specification 1.1 and the Docker/Distribution token authentication flow it relies on (`WWW-Authenticate: Bearer realm=...,service=...,scope=...`, token endpoint answering JSON `token` or `access_token`). Extensions never reference `Curl.Protocol.*` or another extension, so the HTTP/1.1 exchange is written here; see `Curl.Extension.S3.UnitLibrary/Http` and `Curl.Extension.OAuth2.UnitLibrary/Http` for the shape (Content-Length, chunked and close-delimited bodies). Use the `new-project` skill; the solution line goes directly under the extension marker comment in `Curl.slnx` (ADR-1003). Do not register in `ExtensionCatalogue.cs` yet (BL-4292 does).

## Acceptance criteria

- [ ] `Curl.Extension.OciRegistry.UnitLibrary` and `Curl.Extension.OciRegistry.UnitTests` exist, listed in `/Extensions/OciRegistry/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] An HTTP/1.1 exchange writes a request (method, path, Host, headers, optional body) to an `IConnection` and reads the status, headers and a Content-Length, chunked or close-delimited body; unit tests with fake connections.
- [ ] A `WWW-Authenticate: Bearer` challenge is parsed (realm, service, scope, quoted values with commas) and the token request is built and its JSON answer (`token`, else `access_token`) read, with optional Basic credentials from `-u`; unit tests cite the token authentication flow.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8f785ad2 499bd58d 00a80bd4 3180db99 3d836d5e.
