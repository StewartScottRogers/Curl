---
id: BL-4102
title: Extension: Call gRPC services with grpc:// and grpcs://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4099]
touches: [Curl.Extension.Grpc.UnitLibrary, Curl.Extension.Grpc.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4102 — Extension: Call gRPC services with grpc:// and grpcs://

## Goal

Curl provides `grpc://` and `grpcs://` URLs; request message from `-d` JSON with a `.proto` file or server reflection, as the extension `Curl.Extension.Grpc.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 1 of 63. Specification: gRPC over HTTP/2 protocol spec (grpc.io PROTOCOL-HTTP2.md), server reflection v1. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.Grpc.UnitLibrary` and `Curl.Extension.Grpc.UnitTests` exist in `/Extensions/Grpc/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `grpc://` and `grpcs://` URLs; request message from `-d` JSON with a `.proto` file or server reflection.
- [ ] Unit tests check its wire behaviour against named sections of gRPC over HTTP/2 protocol spec (grpc.io PROTOCOL-HTTP2.md), server reflection v1, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b2bf8f98 31a72d1f 99ed0a8d bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
