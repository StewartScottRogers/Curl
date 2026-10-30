---
id: BL-4104
title: Extension: Call Connect protocol services
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4099]
touches: [Curl.Extension.ConnectRpc.UnitLibrary, Curl.Extension.ConnectRpc.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4104 — Extension: Call Connect protocol services

## Goal

Curl provides an option that sends a Connect unary or streaming call, as the extension `Curl.Extension.ConnectRpc.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 3 of 63. Specification: Connect protocol reference (connectrpc.com/docs/protocol). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.ConnectRpc.UnitLibrary` and `Curl.Extension.ConnectRpc.UnitTests` exist in `/Extensions/ConnectRpc/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that sends a Connect unary or streaming call.
- [x] Unit tests check its wire behaviour against named sections of Connect protocol reference (connectrpc.com/docs/protocol), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Plan (ADR-1127): `--connect-rpc <unary|stream>`, a transfer hook over curl's HTTP stack like `--jsonrpc` (ADR-1028). The `-d` message (`{}` when none) goes as `application/json` for a unary call, the response written unchanged; a streaming call sends each JSON value of the body in its own envelope as `application/connect+json` and writes each reply message on its own line, exit 22 when the end-of-stream message carries an error, exit 8 for a body that is not a Connect stream or holds a compressed message. `Connect-Protocol-Version: 1` always, `Connect-Timeout-Ms` from `--max-time`. Left out (ADR): protobuf codec, compression, unary GET, trailers.
- touches: added the option, description and transfer hook catalogues of `Curl.Extension.Composition`; they are merge=union shared list files (one line each) where an option extension registers, as BL-4106 did.
- Criterion 1: `--connect-rpc` adds no URL scheme, so `ExtensionCatalogue.cs` (protocol handler factories) gets no line; the extension is registered by one line in each of the three catalogues above, as `--jsonrpc` is. The build (`VerifyExtensionIsolation`) passes.
- Criterion 3: wire tests name the reference's sections (Unary-Request, Streaming-Request/Response, Envelope, End-Stream Message, Error Codes, Timeout) and use recorded bytes through a fake `next`, no network.
- Criterion 4: tests check the option is absent from `ExtensionCommandLineOptions.For` with `CURL_EXTENSIONS=0` (the parser then refuses it as an unknown option, exit 2) and that no hook activates without the option.
- Measured: `Curl.Extension.ConnectRpc.UnitLibrary` 100/100, worst CRAP 10, 0 failing (after splitting `ExecuteAsync` from complexity 12); `Curl.Extension.Composition.UnitLibrary` 100/100, 0 failing. Fast tests green on Windows; the tests are platform-neutral (no paths, OS text or certificates), which meets the Linux and macOS side.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0ae51af6 d7ffb41d 11ed0e93 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --connect-rpc sends Connect unary and streaming calls with JSON messages and writes the reply messages one per line
