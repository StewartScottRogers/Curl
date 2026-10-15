---
id: BL-2286
title: Move the authentication and security-context sources from Curl.Console to Curl.Engine.UnitLibrary
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2285]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2286 — Move the authentication and security-context sources from Curl.Console to Curl.Engine.UnitLibrary

## Goal

The authentication and security-context sources live in `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`), with their tests in `Curl.Engine.UnitTests`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2285, same rules (mechanical move, a file that references a tool-side type stays with a reason in Notes, `CLAUDE.md` paragraphs move with the code). Files (from `Curl.Console`, as of 2026-10-15): `AwsSigV4HttpAuthenticator`, `HttpSignatureHttpAuthenticator`, `HttpSignatureSetup`, `HandBuiltKerberosSources`, `HandBuiltNtlmSecurityContextFactory`, `KerberosDiskFileReader`, `KerberosDiskFileWriter`, `LateBoundSecurityContextFactory`, `NegotiateOptionsMapping`, `Socks5AuthenticationMapping`, `TransferCredentialLookup` (netrc, which libcurl reads for `CURLOPT_NETRC`), `NoCryptoEngines`.

## Acceptance criteria

- [x] None of the files named in Context (other than any kept with a reason in Notes) remains in `Curl.Console`; each is in `Curl.Engine.UnitLibrary` in namespace `Curl.Engine`.
- [x] Their tests are in `Curl.Engine.UnitTests` with the same test names and assertions; none was deleted.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes
- Moved 11 of the 12 files to `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`) with 7 of their test files to `Curl.Engine.UnitTests`.
- `NoCryptoEngines` moved, but `NoCryptoEnginesTests` stays in `Curl.Console.UnitTests`: it drives `CurlCommandRunner`. `Socks5AuthenticationMappingTests` stays too: it uses `CurlComposition`. Names and assertions unchanged.
- `HttpSignatureSetup`, `NegotiateOptionsMapping` and `TransferCredentialLookup` had no same-named test file; their tests are the `CurlCommandRunner*` and `CurlComposition*` tests, which stay (tool-side types).
- `Curl.Engine` quality: 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Authentication and security-context sources moved to Curl.Engine; build, fast tests and quality green
