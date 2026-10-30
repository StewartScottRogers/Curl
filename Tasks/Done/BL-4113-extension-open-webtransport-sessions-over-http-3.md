---
id: BL-4113
title: Extension: Open WebTransport sessions over HTTP/3
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4101, BL-4222, BL-4223]
touches: [Curl.Extension.WebTransport.UnitLibrary, Curl.Extension.WebTransport.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Quic.UnitLibrary/Curl.Quic.UnitLibrary.csproj, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4113 — Extension: Open WebTransport sessions over HTTP/3

## Goal

Curl provides `webtransport://` (or an option on `https://`): bidirectional and unidirectional streams and datagrams, as the extension `Curl.Extension.WebTransport.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 12 of 63. Specification: draft-ietf-webtrans-http3 (latest) and RFC 9297. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.WebTransport.UnitLibrary` and `Curl.Extension.WebTransport.UnitTests` exist in `/Extensions/WebTransport/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `webtransport://` (or an option on `https://`): bidirectional and unidirectional streams and datagrams.
- [x] Unit tests check its wire behaviour against named sections of draft-ietf-webtrans-http3 (latest) and RFC 9297, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Lane 7 sizing pass (2026-10-29), before any code: an extension may use Curl.Quic, Curl.Http3 and Curl.HttpDatagram (shared layers) and dial QUIC through IConnector as DnsOverQuicResolver does, and Http3ClientSession already sends extra SETTINGS and reports SETTINGS_ENABLE_CONNECT_PROTOCOL; but Curl.Quic has no DATAGRAM frames (RFC 9221), so no HTTP/3 datagrams, and neither QuicConnection nor Http3ClientSession hands server-opened bidirectional or unknown-type unidirectional streams to a caller (draft-ietf-webtrans-http3 sections 4.2-4.3). Filed BL-4222 (QUIC datagrams) and BL-4223 (peer streams to the caller; depends on BL-4222) and requeued. BL-4144 (MASQUE) will need BL-4222 too.
- Lane 8 (2026-10-29): resumed the cut-off run. Fixed the one hanging test (it sent a lone 0x54 byte, a truncated two-byte varint that Curl.Http3 drops, so the session waited forever; now 0x40 0x54 with no session ID). Split WebTransportRequest.Of, the session-response read and the peer-stream check so every member is within the gates, and added tests for the dial IOException, grease peer streams, the status and stream predicates and the verifier factory. Measure-CodeQuality.ps1 on Curl.Extension.WebTransport.UnitLibrary: 100% line, 100% branch, worst CRAP 10, 0 failing. Scheme, draft-16 and what is left out are in ADR-1077. CI on Linux and macOS: the change touches no path, file system, OS error or certificate store, and the fast tests are green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3b935d4c e17cc91f 35ce08fa bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4222 (QUIC DATAGRAM frames) and BL-4223 (HTTP/3 session hands server-opened streams to its caller)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3b935d4c e17cc91f 35ce08fa bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. webtransport:// opens a WebTransport session over HTTP/3 with bidirectional, unidirectional and datagram exchanges (ADR-1077)
