---
id: BL-4122
title: Extension: Send Redis commands with redis://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Redis.UnitLibrary, Curl.Extension.Redis.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4122 — Extension: Send Redis commands with redis://

## Goal

Curl provides `redis://` and `rediss://` URLs: one command, reply printed, as the extension `Curl.Extension.Redis.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 21 of 63. Specification: RESP3 specification and the redis URI scheme (IANA). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Redis.UnitLibrary` and `Curl.Extension.Redis.UnitTests` exist in `/Extensions/Redis/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `redis://` and `rediss://` URLs: one command, reply printed.
- [x] Unit tests check its wire behaviour against named sections of RESP3 specification and the redis URI scheme (IANA), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 7): Decisions in ADR-1079: the command comes from the URL path (`/[db]/command/arg/...`), `-d`
  appends the last argument, `HELLO 3 [AUTH]` first, replies written as `redis-cli --raw` does, error replies exit 8
  (67 for a refused HELLO with credentials or NOAUTH). Port 6379 for both schemes, as the IANA registrations give.
- Added `ExtensionDescriptionCatalogue.cs` to touches (a shared list file; the description must be listed for
  `--extensions`) and `Curl.Console.UnitTests/CurlCompositionTests.cs` (it lists every connecting handler, so a new
  TCP handler changes it; no task in Doing on origin/work/dark-factory named it).
- Measure-CodeQuality.ps1 -Library Curl.Extension.Redis.UnitLibrary: 100% line, 100% branch, 0 failing members,
  worst CRAP 10. Composition changed only by registration lines. Checked `curl --extensions`, `--ai-help extensions`
  and `CURL_EXTENSIONS=0 curl redis://...` (exit 1, `Protocol "redis" not supported`) on the built binary.
- CI on Linux and macOS: the tests use only in-memory fake connections and no paths, OS text or certificates, so they
  are platform-neutral; fast tests green here on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 22ae693f 02eec068 65b700fd bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. redis:// and rediss:// send one command over RESP3 and print the reply; gates met
