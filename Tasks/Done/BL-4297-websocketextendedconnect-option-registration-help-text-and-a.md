---
id: BL-4297
title: WebSocketExtendedConnect: option, registration, help text and ADR
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195, BL-4298, BL-4295, BL-4296]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Option, Curl.Extension.WebSocketExtendedConnect.UnitTests/Option, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4297 — WebSocketExtendedConnect: option, registration, help text and ADR

## Goal

Curl has an option that opens `ws://`/`wss://` with extended CONNECT on HTTP/2 or HTTP/3, registered, documented and recorded in an ADR.

## Context

Last part of BL-4112. Ties together the HTTP/2 flow (BL-4295) and the HTTP/3 flow (BL-4296) on the wire layer (BL-4298). Extension rules: ADR-0999 and CLAUDE.md "Extensions".

## Acceptance criteria

- [x] The extension is registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that opens `ws://`/`wss://` with extended CONNECT on HTTP/2 or HTTP/3 instead of the HTTP/1.1 upgrade; unit tests.
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its option names, the specification versions followed (RFC 8441, RFC 9220), and anything left out with why, with its row in `Documentation/Planning/Decisions/README.md`.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).
- Plan: `--ws-extended-connect <h2|h3>` is a transfer hook (`Option/ExtendedConnectTransferHook.cs`) that, for ws/wss URLs only, gets a connection from the host's connector (ALPN h2 over TCP, or h3 over QUIC), opens the WebSocket with BL-4295's or BL-4296's flow and runs it with `ExtendedConnectSession` (send -d as one text message, write data payloads, pong pings, echo the close). Decided in ADR-1111.
- Registration: an option-only extension adds no scheme, so like `--ws-subprotocol` it registers one line each in the option, description and transfer hook catalogues (ADR-1006, ADR-1007) rather than a protocol handler factory in `ExtensionCatalogue.cs`; those three catalogues were added to `touches` (shared list files in `Tasks/SharedListFiles.txt`, so they overlap no other task). The build's `VerifyExtensionIsolation` passes (`dotnet build` clean).
- Checked with the built curl.exe: `curl --extensions` lists ws-extended-connect; `CURL_EXTENSIONS=0 curl --ws-extended-connect h2 ws://127.0.0.1:1/` exits 2 with "option --ws-extended-connect: is unknown"; with extensions on the same command exits 7 (connect refused).
- Measure-CodeQuality.ps1: Curl.Extension.WebSocketExtendedConnect.UnitLibrary 100/100, worst CRAP 10, 0 failing; Curl.Extension.Composition.UnitLibrary 100/100, 0 failing. Fast tests green on Windows; tests use in-memory fakes only, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 07dc8f5e 5478781e e1dc305c a2918b8f 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --ws-extended-connect h2|h3 opens ws:// and wss:// with extended CONNECT over HTTP/2 or HTTP/3, registered and documented (ADR-1111)
