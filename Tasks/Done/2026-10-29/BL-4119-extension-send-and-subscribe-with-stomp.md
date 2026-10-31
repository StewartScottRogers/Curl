---
id: BL-4119
title: Extension: Send and subscribe with STOMP
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Stomp.UnitLibrary, Curl.Extension.Stomp.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4119 — Extension: Send and subscribe with STOMP

## Goal

Curl provides `stomp://` and `stomps://` URLs, as the extension `Curl.Extension.Stomp.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 18 of 63. Specification: STOMP 1.2. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Stomp.UnitLibrary` and `Curl.Extension.Stomp.UnitTests` exist in `/Extensions/Stomp/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `stomp://` and `stomps://` URLs.
- [x] Unit tests check its wire behaviour against named sections of STOMP 1.2, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 3): Decisions in ADR-1059: `stomp` on 61613 (IANA) and `stomps` on 61614 (RabbitMQ's TLS port; STOMP names none); the URL path, decoded, is the destination; `-d` sends SEND then a receipted DISCONNECT, no body SUBSCRIBEs (ack auto) until the server closes; STOMP 1.2 only, no heart-beating; ERROR or an unexpected frame is exit 8. Transactions, other ack modes and custom SEND headers are left out (no curl option maps onto them).
- Added `ExtensionDescriptionCatalogue.cs` to touches: it is the registration `curl --extensions` reads (a shared list file, one line). Added `Curl.Console.UnitTests/CurlCompositionTests.cs`: its test lists every TCP handler receiving the pooling connector, so STOMP's handler joins that list. No task in Doing on origin/work/dark-factory named either.
- Measure-CodeQuality.ps1: Curl.Extension.Stomp.UnitLibrary and Curl.Extension.Composition.UnitLibrary 100/100, 0 failing members. Full build clean, fast tests green on Windows; the tests use only in-memory fakes and no paths, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2c907c17 c9bee9f3 d03c5e3f bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. stomp:// and stomps:// send with -d and subscribe without, over STOMP 1.2
