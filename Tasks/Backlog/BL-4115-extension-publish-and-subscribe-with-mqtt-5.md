---
id: BL-4115
title: Extension: Publish and subscribe with MQTT 5
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Mqtt5.UnitLibrary, Curl.Extension.Mqtt5.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4115 — Extension: Publish and subscribe with MQTT 5

## Goal

Curl provides `mqtt5://`, `mqtt5s://` and `mqtt5+ws://` URLs (curl's `mqtt://` stays 3.1.1): properties, reason codes, shared subscriptions, as the extension `Curl.Extension.Mqtt5.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 14 of 63. Specification: OASIS MQTT Version 5.0. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.Mqtt5.UnitLibrary` and `Curl.Extension.Mqtt5.UnitTests` exist in `/Extensions/Mqtt5/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `mqtt5://`, `mqtt5s://` and `mqtt5+ws://` URLs (curl's `mqtt://` stays 3.1.1): properties, reason codes, shared subscriptions.
- [ ] Unit tests check its wire behaviour against named sections of OASIS MQTT Version 5.0, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
