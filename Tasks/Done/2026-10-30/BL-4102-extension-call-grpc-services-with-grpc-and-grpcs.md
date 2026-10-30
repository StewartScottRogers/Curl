---
id: BL-4102
title: Extension: Call gRPC services with grpc:// and grpcs://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4099, BL-4340, BL-4341, BL-4342]
touches: [Curl.Extension.Grpc.UnitLibrary, Curl.Extension.Grpc.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4102 — Extension: Call gRPC services with grpc:// and grpcs://

## Goal

Curl provides `grpc://` and `grpcs://` URLs; request message from `-d` JSON with a `.proto` file or server reflection, as the extension `Curl.Extension.Grpc.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 1 of 63. Specification: gRPC over HTTP/2 protocol spec (grpc.io PROTOCOL-HTTP2.md), server reflection v1. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Grpc.UnitLibrary` and `Curl.Extension.Grpc.UnitTests` exist in `/Extensions/Grpc/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `grpc://` and `grpcs://` URLs; request message from `-d` JSON with a `.proto` file or server reflection.
- [x] Unit tests check its wire behaviour against named sections of gRPC over HTTP/2 protocol spec (grpc.io PROTOCOL-HTTP2.md), server reflection v1, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 1): Split for size before any code, as the Notes above anticipated: a new project with the wire format, a unary HTTP/2 client flow with `--proto`, and server reflection v1 are three runs at 100% coverage. Every hook they need exists (`ProtocolHandlerServices` and `ExtensionCatalogue` as Coap uses them, `Curl.Http2.UnitLibrary` `Http2Connection`, `Curl.Protobuf.UnitLibrary` descriptors and JSON mapping). Filed BL-4340 (project and `Wire/`), BL-4341 (`Client/`, registration, `--extensions`, `CURL_EXTENSIONS=0`, ADR; depends on BL-4340) and BL-4342 (`Reflection/`; depends on both), each in its own subfolder. Once all three are Done this task only verifies its criteria against the finished library and closes.

- 2026-10-30 (lane 1): Verified against the finished library; no code changed. Project and registration: `/Extensions/Grpc/` in Curl.slnx, one line in ExtensionCatalogue.cs, full `dotnet build` clean (VerifyExtensionIsolation passes). Schemes and `-d` JSON with `--grpc-proto` or server reflection: BL-4341 and BL-4342, unit tests over scripted connections naming PROTOCOL-HTTP2.md sections. `curl --extensions` and `curl --ai-help extensions` list `grpc`; `CURL_EXTENSIONS=0 curl grpc://localhost/a.B/C` prints `curl: (1) Protocol "grpc" not supported` and exits 1 (pinned by BL-4341's unit test). ADRs: ADR-1133 (wire), ADR-1147 (names `grpc`/`grpcs`/`--grpc-proto`, grpc.io documents as of October 2026, what is not sent: compression, user-agent), ADR-1149 (reflection v1). Quality gates: BL-4342's Measure-CodeQuality.ps1 run (100% line and branch, max complexity 10, no failing member) is on the same code (569fa0d39 is the last change to the library), so it was not rerun. Fast tests green; tests are in-memory and platform-neutral, so CI on Linux and macOS is covered.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b2bf8f98 31a72d1f 99ed0a8d bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size into BL-4340, BL-4341 and BL-4342; waits on them
- 2026-10-30: Backlog -> Doing. Criteria at claim: b2bf8f98 31a72d1f 99ed0a8d bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. Every criterion met by BL-4340, BL-4341 and BL-4342; verified
