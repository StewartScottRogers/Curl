---
id: BL-4106
title: Extension: Send JSON-RPC 2.0 calls with --jsonrpc
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.JsonRpc.UnitLibrary, Curl.Extension.JsonRpc.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4106 — Extension: Send JSON-RPC 2.0 calls with --jsonrpc

## Goal

Curl provides `--jsonrpc <method> <params>` with ids, batches and reply pairing, as the extension `Curl.Extension.JsonRpc.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 5 of 63. Specification: JSON-RPC 2.0 specification. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.JsonRpc.UnitLibrary` and `Curl.Extension.JsonRpc.UnitTests` exist in `/Extensions/JsonRpc/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--jsonrpc <method> <params>` with ids, batches and reply pairing.
- [x] Unit tests check its wire behaviour against named sections of JSON-RPC 2.0 specification, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Plan (ADR-1028): `--jsonrpc` takes one value, `<method> [params]` calls separated by `;` (a batch), `!` before a method for a notification, ids 1, 2, 3 for the rest. A transfer hook replaces the HTTP body with an `application/json` POST and writes the replies paired by id, one per line in call order, then the unpaired ones. An extension option takes no value or exactly one (`ExtensionOptionArgument`, last value wins), so everything goes in one value; decided rather than widening `Curl.Extension.Abstractions` and the parser.
- touches: added the option, description and transfer hook catalogues of `Curl.Extension.Composition`. They are merge=union shared list files (one line each) where an option extension registers.
- Criterion 1: `--jsonrpc` adds no URL scheme, so `ExtensionCatalogue.cs` (protocol handler factories) gets no line; the extension is registered by one line in each of the three catalogues above (ADR-1006, ADR-1007, ADR-1008). The build (`VerifyExtensionIsolation`) passes.
- Criterion 4: tests check the option is absent from `ExtensionCommandLineOptions.For` with `CURL_EXTENSIONS=0` (the parser then refuses it as an unknown option, exit 2, tested in `Curl.Console`) and that no hook activates without the option.
- Measured: `Curl.Extension.JsonRpc.UnitLibrary` 100/100, worst CRAP 10, 0 failing (after splitting `ReadCall` from complexity 20); `Curl.Extension.Composition.UnitLibrary` 100/100, 0 failing. Fast tests green on Windows; the tests are platform-neutral (no paths, OS text or certificates), which meets the Linux and macOS side.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: dc5a3e3d 173ef5a2 3e7398a2 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --jsonrpc sends JSON-RPC 2.0 calls and batches over HTTP and pairs the replies with their calls by id
