---
id: BL-4115
title: Extension: Publish and subscribe with MQTT 5
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4242, BL-4243, BL-4244]
touches: [Curl.Extension.Mqtt5.UnitLibrary, Curl.Extension.Mqtt5.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4115 — Extension: Publish and subscribe with MQTT 5

## Goal

Curl provides `mqtt5://`, `mqtt5s://` and `mqtt5+ws://` URLs (curl's `mqtt://` stays 3.1.1): properties, reason codes, shared subscriptions, as the extension `Curl.Extension.Mqtt5.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 14 of 63. Specification: OASIS MQTT Version 5.0. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Mqtt5.UnitLibrary` and `Curl.Extension.Mqtt5.UnitTests` exist in `/Extensions/Mqtt5/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `mqtt5://`, `mqtt5s://` and `mqtt5+ws://` URLs (curl's `mqtt://` stays 3.1.1): properties, reason codes, shared subscriptions.
- [x] Unit tests check its wire behaviour against named sections of OASIS MQTT Version 5.0, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29, lane 6 prerequisite pass (before any code): too large for one run, so it is split like SignalR (BL-4218/BL-4219) into BL-4242 (projects and MQTT 5 packet wire format: data types, properties, reason codes, every packet, shared-subscription and topic validation), BL-4243 (publish/subscribe client handler over `IConnection` for `mqtt5://` and `mqtt5s://`, following curl's `mqtt://` behaviour) and BL-4244 (`mqtt5+ws://` over the shared `Curl.WebSocket.UnitLibrary` frame codec). Each subtask works in its own folder (`Packets/`/`Encoding/`, `Client/`, `WebSocket/`). What is left here once they are Done: the `ExtensionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs` lines (the latter added to `touches`, needed for the `--extensions` criterion; it is a shared list file, one line per entry), the ADR for the scheme names, specification version and omissions, the `CURL_EXTENSIONS=0` test and the quality run.

- 2026-10-29, lane 2 (resumed run): registered `Mqtt5SchemeHandler` (one handler serving all three schemes, so the extension stays one catalogue line) and the description line; ADR-1085 records the scheme names, ports, MQTT 5.0 and omissions. Added `Curl.Console.UnitTests/CurlCompositionTests.cs` to touches: its pinned list of connecting handlers must name the new handler, and no task in Doing on work/dark-factory touches it. `CURL_EXTENSIONS=0` is pinned by `Mqtt5ExtensionTests.Dispatch_WithCurlExtensions0_FailsAsCurlFailsAnUnsupportedScheme`; `--extensions`/`--ai-help extensions` by `ExtensionListing_WithExtensionsOn_ListsMqtt5InBothForms`. Measure-CodeQuality.ps1 for Mqtt5 and Composition: 0 failing members. Fast tests green on Windows; the tests use no paths, OS text or certificates, so they are platform-neutral (CI criterion met that way).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 439bb925 38df124b dd649eb0 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size before any code: waits on BL-4242 (wire format), BL-4243 (mqtt5/mqtt5s client) and BL-4244 (mqtt5+ws)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 439bb925 38df124b dd649eb0 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. MQTT 5 extension registered for mqtt5, mqtt5s and mqtt5+ws, ADR-1085, gates and fast tests green
