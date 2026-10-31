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
completed: 2026-10-29
---
# BL-4291 — OCI extension: HTTP/1.1 exchange and registry Bearer token auth

## Goal

`Curl.Extension.OciRegistry.UnitLibrary` exists with an HTTP/1.1 request/response exchange over an `IConnection` and the registry token flow, so the pull (BL-4292) and push (BL-4293) flows can talk to a registry.

## Context

Split from BL-4127 (extension 26 of 63; ADR-0999, CLAUDE.md "Extensions"). Specification: OCI Distribution Specification 1.1 and the Docker/Distribution token authentication flow it relies on (`WWW-Authenticate: Bearer realm=...,service=...,scope=...`, token endpoint answering JSON `token` or `access_token`). Extensions never reference `Curl.Protocol.*` or another extension, so the HTTP/1.1 exchange is written here; see `Curl.Extension.S3.UnitLibrary/Http` and `Curl.Extension.OAuth2.UnitLibrary/Http` for the shape (Content-Length, chunked and close-delimited bodies). Use the `new-project` skill; the solution line goes directly under the extension marker comment in `Curl.slnx` (ADR-1003). Do not register in `ExtensionCatalogue.cs` yet (BL-4292 does).

## Acceptance criteria

- [x] `Curl.Extension.OciRegistry.UnitLibrary` and `Curl.Extension.OciRegistry.UnitTests` exist, listed in `/Extensions/OciRegistry/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [x] An HTTP/1.1 exchange writes a request (method, path, Host, headers, optional body) to an `IConnection` and reads the status, headers and a Content-Length, chunked or close-delimited body; unit tests with fake connections.
- [x] A `WWW-Authenticate: Bearer` challenge is parsed (realm, service, scope, quoted values with commas) and the token request is built and its JSON answer (`token`, else `access_token`) read, with optional Basic credentials from `-u`; unit tests cite the token authentication flow.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Pipeline run directly (no subagents) to stay inside the run's budget: the HTTP/1.1 exchange is `Curl.Extension.OAuth2.UnitLibrary/Http` renamed and generalised (any method, ordered fields, optional `byte[]` body), as the extension isolation rule forbids sharing it.
- Decided by Claude under Stewart's delegation (defaults, no ADR needed): the whole body is held in memory with a 1 GiB cap (`OciHttpBodyReader.MaxBodyLength`, exit 63 past it), as a blob pull may be large; BL-4292 may stream blobs instead. A response to `HEAD` reads no body (RFC 9110 §9.3.2), for the push flow's blob existence checks. Method and field names must be tokens and values free of CR/LF, else `InvalidRequestField` (exit 43) before a byte is written.
- Token flow: `OciBearerChallengeParser.Parse` (scheme case-insensitive, token or quoted-string auth-params with backslash escapes, first duplicate wins, realm required), `OciTokenRequestBuilder.Build` (GET realm with escaped `service` and `scope` appended to any realm query, `Accept: application/json`, `Authorization: Basic` from `-u`'s `user:password`), `OciTokenResponseParser.ParseToken` (`token`, else `access_token`, non-empty strings only).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.OciRegistry.UnitLibrary` reports 100% line, 100% branch, max complexity 10, 0 failing members; 74 unit tests; the full fast run green. Tests are platform-neutral (recorded bytes only, no paths or OS text), which covers CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8f785ad2 499bd58d 00a80bd4 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. OciRegistry extension library has an HTTP/1.1 registry exchange and the Bearer token challenge, request and answer
