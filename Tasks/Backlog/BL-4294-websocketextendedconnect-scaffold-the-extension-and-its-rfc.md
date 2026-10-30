---
id: BL-4294
title: WebSocketExtendedConnect: scaffold the extension and its RFC 8441/9220 wire format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Wire, Curl.Extension.WebSocketExtendedConnect.UnitTests/Wire, Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Curl.Extension.WebSocketExtendedConnect.UnitLibrary.csproj, Curl.Extension.WebSocketExtendedConnect.UnitTests/Curl.Extension.WebSocketExtendedConnect.UnitTests.csproj, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4294 — WebSocketExtendedConnect: scaffold the extension and its RFC 8441/9220 wire format

## Goal

The extension projects exist and a pure, network-free wire layer builds and parses the RFC 8441 and RFC 9220 extended CONNECT messages.

## Context

First part of BL-4112 (extension 11 of 63; rules in ADR-0999 and CLAUDE.md "Extensions"). Specification: RFC 8441 (HTTP/2) and RFC 9220 (HTTP/3). Use the `new-project` skill. Registration in `ExtensionCatalogue.cs` waits for BL-4297, which adds the option.

## Acceptance criteria

- [ ] `Curl.Extension.WebSocketExtendedConnect.UnitLibrary` and `Curl.Extension.WebSocketExtendedConnect.UnitTests` exist in `/Extensions/WebSocketExtendedConnect/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] A wire type in `Wire/` builds the extended CONNECT request header list (`:method` CONNECT, `:protocol` websocket, `:scheme`, `:path`, `:authority`, `sec-websocket-version: 13`, optional subprotocols) as RFC 8441 section 4 and RFC 9220 section 3 specify, and a unit test per named section checks it.
- [ ] A wire type in `Wire/` reads SETTINGS_ENABLE_CONNECT_PROTOCOL (0x8 for HTTP/2, RFC 8441 section 3; 0x08 for HTTP/3, RFC 9220 section 3) and validates the 2xx response of an extended CONNECT; unit tests cover advertised, absent and value 0, and accepted and refused responses.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).
- Continues the stashed work of BL-4112.

## Log

- 2026-10-29: Created.
