---
id: BL-2404
title: Libcurl: SetOption for the authentication options (EasyAuthenticationOptions)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2399]
touches: [Libcurl/Options/Authentication, Libcurl.UnitTests/Options/Authentication]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2404 — Libcurl: SetOption for the authentication options (EasyAuthenticationOptions)

## Goal

Every `CURLOPT_*` of `EasyAuthenticationOptions` (netrc, user and password, `HTTPAUTH`/`PROXYAUTH` bitmasks, SASL, bearer, service names, `AWS_SIGV4`, TLS-SRP and the proxy twins) is settable and in the options table.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per option; the `CURLAUTH_*` bitmask maps to `HttpAuthenticationMethods` and an unknown bit returns `BadFunctionArgument`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- The rows are in `Libcurl/Options/Authentication` (`EasyAuthenticationOptionSetters`) and the tests in `Libcurl.UnitTests/Options/Authentication`. The decisions are in ADR-0574.
- "Unknown bit" is read as a bit that `HttpAuthenticationMethods` cannot hold, which answers `BadFunctionArgument`. That is anything beyond 32 bits; a sign-extended 32-bit value still fits, so `CURLAUTH_ANY` works as both -17 and 4294967279. Unnamed bits 8 to 30 are kept, as upstream's `httpauth()` keeps them. `CURLAUTH_ONLY` on its own answers `NotBuiltIn`, as upstream does.
- `Libcurl/CLAUDE.md` is unchanged: nothing it says became untrue, and it is outside `touches`. No new public members.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members. `Libcurl.UnitTests` passed 370 tests. The build is clean and the fast tests are green. `PassingUpstreamCases.txt` is unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Every EasyAuthenticationOptions CURLOPT_* is settable through SetOption, CURLAUTH bitmasks map to HttpAuthenticationMethods
