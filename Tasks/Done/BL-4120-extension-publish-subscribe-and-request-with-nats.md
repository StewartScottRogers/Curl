---
id: BL-4120
title: Extension: Publish, subscribe and request with NATS
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Nats.UnitLibrary, Curl.Extension.Nats.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4120 — Extension: Publish, subscribe and request with NATS

## Goal

Curl provides `nats://` and `natss://`-style URLs (scheme names decided in the ADR), as the extension `Curl.Extension.Nats.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 19 of 63. Specification: NATS client protocol. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Nats.UnitLibrary` and `Curl.Extension.Nats.UnitTests` exist in `/Extensions/Nats/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `nats://` and `natss://`-style URLs (scheme names decided in the ADR).
- [x] Unit tests check its wire behaviour against named sections of NATS client protocol, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Decisions (ADR-1065, renumbered from 1059, which BL-4119's STOMP ADR took first, then from 1062, which BL-4257's AMQP 1.0 ADR took first): schemes `nats://` and `natss://`, port 4222; `natss://` is TLS from the first byte (server `handshake_first`), because `IConnection` cannot start TLS over an open connection. No `-d` subscribes (each payload and a newline, `?count=N` ends after N), `-d` publishes and waits for the closing PONG, `?request` publishes with an `_INBOX` reply subject and writes the one reply.
- Added `ExtensionDescriptionCatalogue.cs` to touches: `curl --extensions` and `--ai-help extensions` list from it (a shared list file; no Doing task held it). Added `Curl.Console.UnitTests/CurlCompositionTests.cs`: its test of every connecting handler names each one, so NATS had to be added (no Doing task held it; on the shared branch BL-4119 already added STOMP there, so the integration merges both names and makes the count 17).
- The wire-format tests are one class, `NatsWireTests`, across the small wire types (`NatsConnect`, `NatsMessageLine`, `NatsServerInfo`, `NatsSubject`, `NatsOperations`, `NatsWireReader`), each checked against its named specification section.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Nats.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 8. Real binary: `curl --extensions` lists nats; `CURL_EXTENSIONS=0 curl nats://127.0.0.1/a` gives exit 1 `Protocol "nats" not supported`.
- CI on Linux and macOS: the tests use no paths, OS error text or certificates, only scripted bytes, so they are platform-neutral; fast tests green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9f286a62 a2a765d0 92f7a699 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. NATS extension: nats:// and natss:// subscribe, publish and request, registered and described, ADR-1065, gates 100/100
