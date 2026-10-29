---
id: BL-4108
title: Extension: Connect to SignalR hubs with signalr://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.SignalR.UnitLibrary, Curl.Extension.SignalR.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4108 — Extension: Connect to SignalR hubs with signalr://

## Goal

Curl provides `signalr://` and `signalrs://` URLs: negotiate, then invoke or stream over WebSocket with the JSON hub protocol, as the extension `Curl.Extension.SignalR.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 7 of 63. Specification: ASP.NET Core SignalR hub protocol and transport specs. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.SignalR.UnitLibrary` and `Curl.Extension.SignalR.UnitTests` exist in `/Extensions/SignalR/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `signalr://` and `signalrs://` URLs: negotiate, then invoke or stream over WebSocket with the JSON hub protocol.
- [ ] Unit tests check its wire behaviour against named sections of ASP.NET Core SignalR hub protocol and transport specs, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
