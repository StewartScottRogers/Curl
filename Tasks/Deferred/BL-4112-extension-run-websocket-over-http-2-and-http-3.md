---
id: BL-4112
title: Extension: Run WebSocket over HTTP/2 and HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary, Curl.Extension.WebSocketExtendedConnect.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4112 — Extension: Run WebSocket over HTTP/2 and HTTP/3

## Goal

Curl provides an option that opens `ws://`/`wss://` with extended CONNECT on HTTP/2 or HTTP/3 instead of the HTTP/1.1 upgrade, as the extension `Curl.Extension.WebSocketExtendedConnect.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 11 of 63. Specification: RFC 8441 and RFC 9220. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.WebSocketExtendedConnect.UnitLibrary` and `Curl.Extension.WebSocketExtendedConnect.UnitTests` exist in `/Extensions/WebSocketExtendedConnect/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option that opens `ws://`/`wss://` with extended CONNECT on HTTP/2 or HTTP/3 instead of the HTTP/1.1 upgrade.
- [ ] Unit tests check its wire behaviour against named sections of RFC 8441 and RFC 9220, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 (lane 3, first pass, no code written): three prerequisites found and filed. (1) BL-4196: the WebSocket frame codec is only in Curl.Original.Protocol.Ws.UnitLibrary, which an extension may not reference (ADR-0999). (2) BL-4194: the HTTP/3 client session (Http3Session, Http3StreamConnection) is only in Curl.Original.Protocol.Http.UnitLibrary; HTTP/2 needs no move, Http2Connection is already in the shared Curl.Http2.UnitLibrary. (3) BL-4195: no extension hook lets an option-activated extension serve ws/wss in place of curl's handler (only transfer and connection hooks exist). Also learned: Curl.Http3 has EnableConnectProtocol (0x08) but Http2SettingIdentifier has no SETTINGS_ENABLE_CONNECT_PROTOCOL (0x8, RFC 8441 section 3); unknown settings are allowed, so this task can send and read 0x8 itself or add the member then.

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 80dd9d14 7bdf3437 50d813ce e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4196 (shared WebSocket codec), BL-4194 (HTTP/3 client session in Curl.Http3) and BL-4195 (extension scheme hook)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 80dd9d14 7bdf3437 50d813ce e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-4112-20261029-140509-L5.jsonl
- 2026-10-29: Backlog -> Deferred. Split into BL-4298, BL-4295, BL-4296 and BL-4297; cost cap exceeded
