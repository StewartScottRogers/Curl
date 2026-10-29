---
id: BL-4108
title: Extension: Connect to SignalR hubs with signalr://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4218, BL-4219]
touches: [Curl.Extension.SignalR.UnitLibrary, Curl.Extension.SignalR.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4108 — Extension: Connect to SignalR hubs with signalr://

## Goal

Curl provides `signalr://` and `signalrs://` URLs: negotiate, then invoke or stream over WebSocket with the JSON hub protocol, as the extension `Curl.Extension.SignalR.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 7 of 63. Specification: ASP.NET Core SignalR hub protocol and transport specs. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.SignalR.UnitLibrary` and `Curl.Extension.SignalR.UnitTests` exist in `/Extensions/SignalR/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `signalr://` and `signalrs://` URLs: negotiate, then invoke or stream over WebSocket with the JSON hub protocol.
- [x] Unit tests check its wire behaviour against named sections of ASP.NET Core SignalR hub protocol and transport specs, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 6, prerequisite pass before any edit): split for size as these Notes ask. BL-4218 creates the projects and the JSON hub protocol and negotiate codec (HubProtocol.md, TransportProtocols.md); BL-4219 adds the negotiate HTTP/1.1 exchange and the RFC 6455 4.1 upgrade over IConnection, using the shared Curl.WebSocket.UnitLibrary frame codec (an extension may reference a shared layer). This task then adds the handler for signalr:// and signalrs:// (TLS through ProtocolHandlerServices.TlsProvider), how a URL or extension option names the hub method and arguments and chooses invoke or stream, the ExtensionCatalogue line, the description and option catalogue lines (added to touches: --extensions reads ExtensionDescriptionCatalogue.cs; both are one-line shared lists in Tasks/SharedListFiles.txt), the ADR, the CURL_EXTENSIONS=0 test and the quality gates. Checked: ExtensionProtocolHandlers.For is already passed to the engine by CurlComposition, and an unregistered scheme fails in TransferPerformer.Setup.cs with exit 1 "Protocol \"x\" not supported", so no engine hook is missing.
- 2026-10-29 (lane 4): ADR-1081 (1075 to 1080 were taken on the shared branch). A handler sees no extension option values, so the call is the `-d` body in the hub message's own shape, `{"type":1|4,"target":...,"arguments":[...]}`; no extension option and so no ExtensionOptionCatalogue line. `signalr`/`signalrs` map to `http`/`https` with ports 80/443. Exit codes: bad `-d` 2, hub-reported errors 22, transport or protocol breaks 8, connect failures the connector's own. Client Pings, MessagePack, SSE/long polling, client streams and stateful reconnect left out (ADR-1081 says why).
- 2026-10-29 (lane 4): added `Curl.Console.UnitTests/CurlCompositionTests.cs` to touches: its list of every connecting handler gains `SignalRProtocolHandler` (count 19 to 20; the shared branch already has 20 with Kafka, so the resolver takes 21). No task in Doing on origin/work/dark-factory named it.
- 2026-10-29 (lane 4): checks run: Measure-CodeQuality.ps1 for SignalR and Composition, 100/100, worst CRAP 10 and 6 after splitting `SignalRHubSession.AcceptAsync` (complexity 14); `curl --extensions` and `--ai-help extensions` list signalr; `CURL_EXTENSIONS=0 curl signalr://example.com/hub` gives `curl: (1) Protocol "signalr" not supported`, exit 1; dotnet build clean, fast tests green (Curl.Console.UnitTests 3095 passed after the handler-list fix). Platform-neutral: no paths, OS errors or certificates in the tests.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3031275f 5e90994b fb363012 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4218 (projects and hub protocol codec) and BL-4219 (negotiate and WebSocket upgrade)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3031275f 5e90994b fb363012 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. signalr:// and signalrs:// negotiate, then invoke or stream a hub method over WebSocket with the JSON hub protocol
