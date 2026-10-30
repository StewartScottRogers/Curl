---
id: BL-4297
title: WebSocketExtendedConnect: option, registration, help text and ADR
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195, BL-4294, BL-4295, BL-4296]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Option, Curl.Extension.WebSocketExtendedConnect.UnitTests/Option, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4297 — WebSocketExtendedConnect: option, registration, help text and ADR

## Goal

Curl has an option that opens `ws://`/`wss://` with extended CONNECT on HTTP/2 or HTTP/3, registered, documented and recorded in an ADR.

## Context

Last part of BL-4112. Ties together the HTTP/2 flow (BL-4295) and the HTTP/3 flow (BL-4296) on the wire layer (BL-4294). Extension rules: ADR-0999 and CLAUDE.md "Extensions".

## Acceptance criteria

- [ ] The extension is registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option that opens `ws://`/`wss://` with extended CONNECT on HTTP/2 or HTTP/3 instead of the HTTP/1.1 upgrade; unit tests.
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its option names, the specification versions followed (RFC 8441, RFC 9220), and anything left out with why, with its row in `Documentation/Planning/Decisions/README.md`.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).

## Log

- 2026-10-29: Created.
