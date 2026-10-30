---
id: BL-4117
title: Extension: Publish and get messages over AMQP 0-9-1
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4259, BL-4260]
touches: [Curl.Extension.Amqp0.UnitLibrary, Curl.Extension.Amqp0.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4117 — Extension: Publish and get messages over AMQP 0-9-1

## Goal

Curl provides `amqp://` and `amqps://` URLs: publish, basic.get, queue declare, as the extension `Curl.Extension.Amqp0.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 16 of 63. Specification: AMQP 0-9-1 specification (RabbitMQ). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Amqp0.UnitLibrary` and `Curl.Extension.Amqp0.UnitTests` exist in `/Extensions/Amqp0/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `amqp://` and `amqps://` URLs: publish, basic.get, queue declare.
- [x] Unit tests check its wire behaviour against named sections of AMQP 0-9-1 specification (RabbitMQ), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29, lane 9 prerequisite pass (before any code): too large for one run, so it is split like BL-4115 (Mqtt5) into BL-4259 (projects and AMQP 0-9-1 wire format: protocol header, frames, data types, field tables, the connection/channel/queue/basic methods and content header, in `Wire/` and `Methods/`) and BL-4260 (handler over `IConnection` for `amqp://` and `amqps://`: negotiation, queue.declare, basic.publish with `-d`, basic.get otherwise, in `Client/`). What is left here once they are Done: the `ExtensionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs` lines (the latter added to `touches`, needed for the `--extensions` criterion; it is a shared list file, one line per entry), the ADR for the scheme names, specification version and omissions, the `CURL_EXTENSIONS=0` test and the quality run.

- 2026-10-29, lane 6: registered by one line in each catalogue (ADR-1091 records the scheme names amqp and amqps, AMQP 0-9-1 with RabbitMQ's errata and what is left out). Added `Curl.Console.UnitTests/CurlCompositionTests.cs` to `touches`: its pinned list of connecting handlers must name `Amqp0ProtocolHandler` now that the extension is registered; no task in Doing on origin/work/dark-factory names that file. The `CURL_EXTENSIONS=0` criterion is `Amqp0ExtensionTests.Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme`. Measure-CodeQuality.ps1: 0 failing members for Curl.Extension.Amqp0.UnitLibrary and for Curl.Extension.Composition.UnitLibrary. Build clean and fast tests green on Windows; the change is platform-neutral (no paths, OS errors or certificates), which meets the CI criterion.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b94d997c 6b67e8fd 8f94f79e bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size before any code: waits on BL-4259 (wire format) and BL-4260 (amqp/amqps client)
- 2026-10-29: Backlog -> Doing. Criteria at claim: b94d997c 6b67e8fd 8f94f79e bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. amqp:// and amqps:// registered as the amqp0 extension, ADR-1091, gates met
