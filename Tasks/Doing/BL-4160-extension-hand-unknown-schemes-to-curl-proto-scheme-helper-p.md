---
id: BL-4160
title: Extension: Hand unknown schemes to curl-proto-<scheme> helper programs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.PluginProtocol.UnitLibrary, Curl.Extension.PluginProtocol.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4160 — Extension: Hand unknown schemes to curl-proto-<scheme> helper programs

## Goal

Curl provides a helper `curl-proto-<scheme>` on PATH handles that scheme; with none, Curl answers as curl does for an unsupported scheme, as the extension `Curl.Extension.PluginProtocol.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 60 of 63. Specification: git's remote-helper model, adapted; the protocol between Curl and the helper is defined in the ADR. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.PluginProtocol.UnitLibrary` and `Curl.Extension.PluginProtocol.UnitTests` exist in `/Extensions/PluginProtocol/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides a helper `curl-proto-<scheme>` on PATH handles that scheme; with none, Curl answers as curl does for an unsupported scheme.
- [ ] Unit tests check its wire behaviour against named sections of git's remote-helper model, adapted; the protocol between Curl and the helper is defined in the ADR, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 33082493 a435a8fc a8348ce6 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
