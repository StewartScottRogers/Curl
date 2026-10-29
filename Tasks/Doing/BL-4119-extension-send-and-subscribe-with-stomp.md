---
id: BL-4119
title: Extension: Send and subscribe with STOMP
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Stomp.UnitLibrary, Curl.Extension.Stomp.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4119 — Extension: Send and subscribe with STOMP

## Goal

Curl provides `stomp://` and `stomps://` URLs, as the extension `Curl.Extension.Stomp.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 18 of 63. Specification: STOMP 1.2. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.Stomp.UnitLibrary` and `Curl.Extension.Stomp.UnitTests` exist in `/Extensions/Stomp/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `stomp://` and `stomps://` URLs.
- [ ] Unit tests check its wire behaviour against named sections of STOMP 1.2, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2c907c17 c9bee9f3 d03c5e3f bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
