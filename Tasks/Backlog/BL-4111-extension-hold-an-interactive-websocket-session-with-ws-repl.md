---
id: BL-4111
title: Extension: Hold an interactive WebSocket session with --ws-repl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4211, BL-4221]
touches: [Curl.Extension.WebSocketRepl.UnitLibrary, Curl.Extension.WebSocketRepl.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4111 — Extension: Hold an interactive WebSocket session with --ws-repl

## Goal

Curl provides `--ws-repl` on a `ws://` or `wss://` URL: type a message, see replies, ping/pong, text or binary, as the extension `Curl.Extension.WebSocketRepl.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 10 of 63. Specification: RFC 6455. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.WebSocketRepl.UnitLibrary` and `Curl.Extension.WebSocketRepl.UnitTests` exist in `/Extensions/WebSocketRepl/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `--ws-repl` on a `ws://` or `wss://` URL: type a message, see replies, ping/pong, text or binary.
- [ ] Unit tests check its wire behaviour against named sections of RFC 6455, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 2, prerequisite pass before any edit): split for size as these Notes ask, and requeued on two prerequisites. (1) BL-4221 creates both projects, the slnx line, the RFC 6455 section 4 opening handshake and the interactive session (text, binary, ping/pong, close) over `IConnection`, using the shared frame codec `Curl.WebSocket.UnitLibrary` (an extension may reference a shared layer; `VerifyExtensionIsolation` only refuses `Curl.Protocol.*` other than Abstractions). (2) `--ws-repl` is an option on curl's own `ws://`/`wss://` schemes, so the extension cannot register a protocol handler for them (that would change curl's behaviour without the option); it must be an option-activated `ITransferHook` whose `ExecuteAsync` skips `next` and runs the session on its own connection. Today a hook gets no `IConnector` (activators are `Func<ExtensionOptionValues, ITransferHook?>`): BL-4211 (Backlog) adds that seam. What is left here: the hook (activator gives the session standard input and the transfer's output; opens the connection through the connector, TLS for `wss`), one line each in the option, description and transfer hook catalogues (added to touches: shared one-line lists in Tasks/SharedListFiles.txt; `ExtensionCatalogue.cs` holds protocol handler factories and gets no line, decide that in the ADR), the ADR, the `CURL_EXTENSIONS=0` exit 2 test, the `--extensions`/`--ai-help extensions` check and the quality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b7a1a892 30d1155b 5dcad200 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4221 (projects, handshake, session) and BL-4211 (connector seam for transfer hooks)
