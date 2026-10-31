---
id: BL-4144
title: Extension: Proxy UDP and IP through MASQUE
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095, BL-4101, BL-4318, BL-4319, BL-4317]
touches: [Curl.Extension.Masque.UnitLibrary, Curl.Extension.Masque.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4144 — Extension: Proxy UDP and IP through MASQUE

## Goal

Curl provides an option using CONNECT-UDP or CONNECT-IP so QUIC and HTTP/3 run through a proxy, as the extension `Curl.Extension.Masque.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 43 of 63. Specification: RFC 9298 and RFC 9484. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.Masque.UnitLibrary` and `Curl.Extension.Masque.UnitTests` exist in `/Extensions/Masque/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option using CONNECT-UDP or CONNECT-IP so QUIC and HTTP/3 run through a proxy.
- [ ] Unit tests check its wire behaviour against named sections of RFC 9298 and RFC 9484, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.
- 2026-10-29 (lane 1, sizing pass before any code): split as the note above asks, and requeued. What the work needs:
  - BL-4318: on the real network the engine's QUIC dialer opens its own UDP sockets (`EngineComposition.Transports.cs` `DialersOf`, `EngineDialing` null), so a connection hook's `DatagramConnectorOver` (ADR-1010) reaches only TFTP today; HTTP/3 cannot run through a MASQUE hook until QUIC dials through it. `QuicDialer.DialThroughTunnelAsync` is internal to `Curl.Networking`, so the engine route is the clean one.
  - BL-4319: the project pair and the wire format (URI templates, HTTP/1.1 upgrade request and reply, CONNECT-IP capsules, IP/UDP packets), on the shared `Curl.HttpDatagram.UnitLibrary` (BL-4101).
  - BL-4317: the datagram connector and channels over curl's connector.
  - Left for this task: the option (planned: one `--masque <uri-template>`, CONNECT-UDP or CONNECT-IP chosen by the template's variables), the `IConnectionHook` keeping curl's TCP connector and replacing the datagram connector, its lines in `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionConnectionHookCatalogue.cs` (added to touches: shared list files, one entry per line; how NamedPipe registers), the ADR, `--extensions`/`--ai-help extensions`, the CURL_EXTENSIONS=0 test and the quality run. `ExtensionCatalogue.cs` (protocol handler factories) likely needs no line, since MASQUE adds no scheme.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: cd391c17 c5786e7e dc0f73b9 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split before any code: waits on BL-4318 (QUIC dials through an extension's datagram connector), BL-4319 (MASQUE wire format) and BL-4317 (MASQUE tunnel client flow)
