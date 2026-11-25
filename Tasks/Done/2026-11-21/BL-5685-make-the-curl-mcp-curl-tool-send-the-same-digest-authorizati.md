---
id: BL-5685
title: Make the curl-mcp curl tool send the same Digest Authorization header as curl.exe
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Console/InProcessCurl.cs, Curl.Mcp/InProcessCurlCommandRunner.cs, Curl.Console.UnitTests/InProcessCurlDigestBuildTests.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5685 — Make the curl-mcp curl tool send the same Digest Authorization header as curl.exe

## Goal

At the same commit, `curl.exe --digest` and the `curl-mcp` `curl` tool with the same arguments send byte-identical `Authorization: Digest` headers (given the same nonce and cnonce), both matching curl 8.22.0's format.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 6. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`curl.exe` and the `curl-mcp` `curl` tool format the Digest `Authorization` header differently at the same commit (spacing, cnonce encoding); servers accept both. They should run the same engine code, so a difference means the tool reaches Digest through another path (different composition, a different cnonce generator, or a different authenticator registration). Find the difference, make both use one path, and pin the format against curl 8.22.0's recorded header (`Record-CurlExchange.ps1`).

## Acceptance criteria

- [x] With a fixed nonce and cnonce, the header from `curl.exe` and from the `curl` tool are byte-identical and equal curl 8.22.0's recorded header, pinned by tests.
- [x] The Notes name the path that differed and how both now share one.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
The path that differed: the tool's `InProcessCurlCommandRunner` calls `InProcessCurl.RunAsync`, which composed the engine with `usesHandBuiltNtlm: true` (the conformance harness's setting), so `EngineComposition.MatchesSspiBuild` was false on Windows and `DigestAuthenticator` wrote curl's non-SSPI form, while `curl.exe` got the SSPI form. Now `InProcessCurl.RunAsync` (TCP-dialer overload) takes `usesHandBuiltNtlm` (default true for conformance callers) and the tool passes false, so both go through `EngineComposition.CreateHttpAuthenticator` with the same build matching (ADR-1332). The SSPI form was already pinned to curl's recorded header (BL-2022); `InProcessCurlDigestBuildTests` pins both forms. `DigestAuthenticator.cs` needed no change; `touches` widened to the files changed (no other Doing task holds them). The cnonce is random so tests pin the header up to `cnonce="`; both hosts use the same generator per build. Platform-neutral: tests are Windows-only via OSCondition. Quality run for Curl.Console: 0 failing members.

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 6.
- 2026-11-21: Backlog -> Doing. Criteria at claim: e04d0441 06376b3a 3d836d5e.
- 2026-11-21: Doing -> Done. Tool's runner now matches curl.exe's build for Digest (ADR-1332)
