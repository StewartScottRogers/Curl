---
id: BL-4118
title: Extension: Send and receive messages over AMQP 1.0
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4258, BL-4256, BL-4257]
touches: [Curl.Extension.Amqp1.UnitLibrary, Curl.Extension.Amqp1.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs]
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

- 2026-10-29, lane 3 prerequisite pass (before any code): too large for one run (AMQP 1.0 type system, transport frames and performatives, SASL, message sections, then a link-level client), so it is split like Mqtt5 (BL-4242/4243) into BL-4258 (projects and the Part 1 type system, `Types/`), BL-4256 (protocol headers, frames, performatives, SASL frames, message sections, `Frames/`) and BL-4257 (send/receive client handler over `IConnection` for `amqp1://` and `amqp1s://`, `Client/`). Scheme names `amqp1`/`amqp1s` (default for the ADR here): BL-4117 (extension 16, AMQP 0-9-1) takes `amqp://`/`amqps://`, and a digit suffix follows the `mqtt5` precedent. What is left here once they are Done: the `ExtensionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs` lines (the latter added to `touches`, needed for the `--extensions` criterion; a shared list file, one line per entry), the ADR for the scheme names, specification version and omissions, the `CURL_EXTENSIONS=0` test and the quality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f471e4b1 8a40d80f 508cee65 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size before any code: waits on BL-4258 (type system), BL-4256 (frames, performatives, SASL) and BL-4257 (amqp1/amqp1s client)
- 2026-10-29: Backlog -> Doing. Criteria at claim: f471e4b1 8a40d80f 508cee65 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
