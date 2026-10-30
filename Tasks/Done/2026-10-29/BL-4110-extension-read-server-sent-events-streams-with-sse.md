---
id: BL-4110
title: Extension: Read Server-Sent Events streams with --sse
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4173, BL-4174, BL-4175]
touches: [Curl.Extension.ServerSentEvents.UnitLibrary, Curl.Extension.ServerSentEvents.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4110 — Extension: Read Server-Sent Events streams with --sse

## Goal

Curl provides `--sse`: parse `text/event-stream`, reconnect with `Last-Event-ID`, honour `retry`, as the extension `Curl.Extension.ServerSentEvents.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 9 of 63. Specification: WHATWG HTML Living Standard, section "Server-sent events". It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.ServerSentEvents.UnitLibrary` and `Curl.Extension.ServerSentEvents.UnitTests` exist in `/Extensions/ServerSentEvents/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--sse`: parse `text/event-stream`, reconnect with `Last-Event-ID`, honour `retry`.
- [x] Unit tests check its wire behaviour against named sections of WHATWG HTML Living Standard, section "Server-sent events", with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 2, prerequisite pass before any edit): `--sse` cannot run yet. (1) No host runs extension transfer hooks: BL-4173 (Backlog) adds `ExtensionTransferHookCatalogue` and the Console wiring; this task registers its hook activator there, plus one line each in `ExtensionOptionCatalogue` and `ExtensionDescriptionCatalogue` (added to touches; all three are shared one-line-per-entry lists). (2) Reconnection (§9.2.3) needs the transfer to run again after `retry` with a fresh `Last-Event-ID` header, and `ITransferHook` (ADR-1007) cannot repeat a transfer: filed BL-4174 for that seam in Curl.Extension.Abstractions. (3) Split for size as these Notes ask: BL-4175 creates the projects and the §9.2.6 parser; this task then adds the option, the hook (parse, reconnect, retry), registration, ADR, --extensions/--ai-help check and quality gates. `ExtensionCatalogue.cs` holds protocol handler factories; SSE adds no scheme, so its "one line" registration lands in the hook catalogue plus the option and description catalogues; decide that in the ADR.
- 2026-10-29 (lane 10): Done as ADR-1067 records. `--sse` flag; `ServerSentEventsTransferHook` adds `Accept: text/event-stream`, `Cache-Control`/`Pragma: no-cache` and `Last-Event-ID` once set, writes the body through `EventStreamWritingStream` (normalised events), calls the new `EventStreamParser.EndStream` when each connection closes, and asks to repeat after `retry` (default 3000 ms, capped at `int.MaxValue` ms) only when a 200 `text/event-stream` response ends. Network errors are not retried (§9.2.3 "futile"). Registration is one line each in the option, hook and description catalogues; `ExtensionCatalogue.cs` is left alone because SSE adds no scheme.
- Checked: `Measure-CodeQuality.ps1 -Library Curl.Extension.ServerSentEvents.UnitLibrary` gives 100% line and branch coverage and 0 failing members (Composition got catalogue lines only). The real binary lists `sse` in `--extensions` and `--ai-help extensions`, and `CURL_EXTENSIONS=0 curl --sse` exits 2 with "option --sse: is unknown". Full build clean; fast tests green apart from 10 `Curl.Quic.UnitTests` tests that hit 2-minute timeouts under load and all pass alone (505/505), filed as BL-4269.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: daf393cf cf7d9fa9 365c043f e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4173 (hook wiring in Curl.Console), BL-4174 (hook can repeat a transfer, for reconnection) and BL-4175 (SSE projects and parser, split for size)
- 2026-10-29: Backlog -> Doing. Criteria at claim: daf393cf cf7d9fa9 365c043f e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --sse parses text/event-stream, reconnects with Last-Event-ID after retry, registered as an extension
