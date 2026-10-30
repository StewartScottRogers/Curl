---
id: BL-4103
title: Extension: Call gRPC-Web services
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4099]
touches: [Curl.Extension.GrpcWeb.UnitLibrary, Curl.Extension.GrpcWeb.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4103 — Extension: Call gRPC-Web services

## Goal

Curl provides an option that sends a gRPC-Web request over HTTP/1.1 or HTTP/2, as the extension `Curl.Extension.GrpcWeb.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 2 of 63. Specification: gRPC-Web protocol (grpc.io PROTOCOL-WEB.md). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.GrpcWeb.UnitLibrary` and `Curl.Extension.GrpcWeb.UnitTests` exist in `/Extensions/GrpcWeb/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that sends a gRPC-Web request over HTTP/1.1 or HTTP/2.
- [x] Unit tests check its wire behaviour against named sections of gRPC-Web protocol (grpc.io PROTOCOL-WEB.md), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (BL-4103 run): touches: added the option, description and transfer hook catalogues of `Curl.Extension.Composition`; they are merge=union shared list files (one line each) where an option extension registers.
- Criterion 1: `--grpc-web` adds no URL scheme, so `ExtensionCatalogue.cs` (protocol handler factories) gets no line; as BL-4106 (`--jsonrpc`), BL-4134 and BL-4139 did, the extension is registered by one line in each of the three catalogues above. The full `dotnet build` (with `VerifyExtensionIsolation`) passes.
- Design (ADR-1127): flag `--grpc-web`; the `-d` data is the request message, sent as one `application/grpc-web+proto` frame with `Accept` and `X-Grpc-Web: 1`; a 200 gRPC-Web reply's messages are written unframed, `grpc-status` from the trailers frame or the headers (trailers-only); non-zero is exit 22, missing or bad framing exit 8, compressed exit 61; any other reply written as received. Left out: grpc-web-text, compression, grpc-timeout, protobuf encoding.
- Size: one run was enough (5 production classes plus the request context, 31 unit tests); no split.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.GrpcWeb.UnitLibrary`: 100% line, 100% branch, max complexity 8, 0 failing members (two methods split after a first run at 12).
- Fast tests green and `dotnet format --verify-no-changes` clean on both new projects. Tests are pure in-memory (no paths, OS errors or certificates), so platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9175bb26 1729ab2e 0eef26a6 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --grpc-web calls a gRPC-Web method over HTTP/1.1 or HTTP/2: framed request, unframed reply messages, grpc-status checked
