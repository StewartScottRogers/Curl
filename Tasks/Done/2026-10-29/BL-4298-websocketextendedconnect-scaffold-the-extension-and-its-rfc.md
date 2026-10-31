---
id: BL-4298
title: WebSocketExtendedConnect: scaffold the extension and its RFC 8441/9220 wire format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Wire, Curl.Extension.WebSocketExtendedConnect.UnitTests/Wire, Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Curl.Extension.WebSocketExtendedConnect.UnitLibrary.csproj, Curl.Extension.WebSocketExtendedConnect.UnitTests/Curl.Extension.WebSocketExtendedConnect.UnitTests.csproj, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4298 — WebSocketExtendedConnect: scaffold the extension and its RFC 8441/9220 wire format

## Goal

The extension projects exist and a pure, network-free wire layer builds and parses the RFC 8441 and RFC 9220 extended CONNECT messages.

## Context

First part of BL-4112 (extension 11 of 63; rules in ADR-0999 and CLAUDE.md "Extensions"). Specification: RFC 8441 (HTTP/2) and RFC 9220 (HTTP/3). Use the `new-project` skill. Registration in `ExtensionCatalogue.cs` waits for BL-4297, which adds the option.

## Acceptance criteria

- [x] `Curl.Extension.WebSocketExtendedConnect.UnitLibrary` and `Curl.Extension.WebSocketExtendedConnect.UnitTests` exist in `/Extensions/WebSocketExtendedConnect/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [x] A wire type in `Wire/` builds the extended CONNECT request header list (`:method` CONNECT, `:protocol` websocket, `:scheme`, `:path`, `:authority`, `sec-websocket-version: 13`, optional subprotocols) as RFC 8441 section 4 and RFC 9220 section 3 specify, and a unit test per named section checks it.
- [x] A wire type in `Wire/` reads SETTINGS_ENABLE_CONNECT_PROTOCOL (0x8 for HTTP/2, RFC 8441 section 3; 0x08 for HTTP/3, RFC 9220 section 3) and validates the 2xx response of an extended CONNECT; unit tests cover advertised, absent and value 0, and accepted and refused responses.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).
- Continues the stashed work of BL-4112.
- The stash held all of BL-4112 (Http2, Http3, option and catalogue registration). This run committed only the scaffold and `Wire/` its `touches` name; the other files stay uncommitted for BL-4295, BL-4296 and BL-4297, and the three Composition catalogue lines wait for BL-4297.
- Wire types: `ExtendedConnectRequestFields` (RFC 8441 s4 / RFC 9220 s3 fields; subprotocols as one comma-separated `sec-websocket-protocol`, the RFC 6455 form), `EnableConnectProtocolSetting` (HTTP/2 0x8 and HTTP/3 0x08; only value 1 enables, absent means 0, the last repeat counts as RFC 9113 s6.5.3 says) and `ExtendedConnectResponse` (any three-digit 2xx `:status` accepts, RFC 9113 s8.5). Sensible defaults, no ADR needed: the RFCs fix every choice.
- Measure-CodeQuality: 100% line and branch, 8 members, worst CRAP 8. Full build clean, fast tests green. Platform-neutral: no paths, OS text or certificates.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5d7725a0 b32aef03 f6c38288 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Scaffold and Wire layer built; gates and fast tests green
