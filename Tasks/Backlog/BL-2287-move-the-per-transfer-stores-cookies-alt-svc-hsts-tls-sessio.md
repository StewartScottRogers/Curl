---
id: BL-2287
title: Move the per-transfer stores (cookies, alt-svc, HSTS, TLS session lines, SSH known hosts) from Curl.Console to Curl.Engine.UnitLibrary
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2286]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2287 — Move the per-transfer stores (cookies, alt-svc, HSTS, TLS session lines, SSH known hosts) from Curl.Console to Curl.Engine.UnitLibrary

## Goal

The per-transfer stores - cookies, alt-svc, HSTS, TLS session lines, SSH known-hosts search, C runtime error numbers - live in `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`), with their tests in `Curl.Engine.UnitTests`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2286, same rules (mechanical move, a file that references a tool-side type stays with a reason in Notes, `CLAUDE.md` paragraphs move with the code). These are what libcurl keeps for `CURLOPT_COOKIEFILE`/`COOKIEJAR`, `ALTSVC`, `HSTS`, `SSH_KNOWNHOSTS` and `CURLINFO_OS_ERRNO`. Files (from `Curl.Console`, as of 2026-10-15): `CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile`, `ClockOverridingHstsStore`, `FixedWallClock`, `TlsSessionFileLines`, `SshKnownHostsFileSearch`, `CRuntimeErrorNumbers`, `CurlErrorBuffer` (`CURLOPT_ERRORBUFFER`'s cut).

## Acceptance criteria

- [ ] None of the files named in Context (other than any kept with a reason in Notes) remains in `Curl.Console`; each is in `Curl.Engine.UnitLibrary` in namespace `Curl.Engine`.
- [ ] Their tests are in `Curl.Engine.UnitTests` with the same test names and assertions; none was deleted.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

## Log

- 2026-10-15: Created.
