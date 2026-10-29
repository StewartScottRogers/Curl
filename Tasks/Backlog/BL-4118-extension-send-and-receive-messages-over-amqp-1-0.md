---
id: BL-4118
title: Extension: Send and receive messages over AMQP 1.0
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Amqp1.UnitLibrary, Curl.Extension.Amqp1.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4118 — Extension: Send and receive messages over AMQP 1.0

## Goal

Curl provides an AMQP 1.0 scheme that does not clash with extension 16 (decided in the ADR), SASL PLAIN/ANONYMOUS, as the extension `Curl.Extension.Amqp1.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 17 of 63. Specification: OASIS AMQP 1.0. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.Amqp1.UnitLibrary` and `Curl.Extension.Amqp1.UnitTests` exist in `/Extensions/Amqp1/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an AMQP 1.0 scheme that does not clash with extension 16 (decided in the ADR), SASL PLAIN/ANONYMOUS.
- [ ] Unit tests check its wire behaviour against named sections of OASIS AMQP 1.0, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
