---
id: BL-4114
title: Extension: Speak graphql-ws, WAMP and STOMP over WebSocket
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.WebSocketSubprotocols.UnitLibrary, Curl.Extension.WebSocketSubprotocols.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4114 — Extension: Speak graphql-ws, WAMP and STOMP over WebSocket

## Goal

Curl provides an option naming a WebSocket subprotocol and framing messages for it, as the extension `Curl.Extension.WebSocketSubprotocols.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 13 of 63. Specification: graphql-transport-ws protocol, WAMP basic profile, STOMP 1.2. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.WebSocketSubprotocols.UnitLibrary` and `Curl.Extension.WebSocketSubprotocols.UnitTests` exist in `/Extensions/WebSocketSubprotocols/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option naming a WebSocket subprotocol and framing messages for it.
- [ ] Unit tests check its wire behaviour against named sections of graphql-transport-ws protocol, WAMP basic profile, STOMP 1.2, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
