---
id: BL-4121
title: Extension: Produce and consume Kafka records
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Kafka.UnitLibrary, Curl.Extension.Kafka.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4121 — Extension: Produce and consume Kafka records

## Goal

Curl provides `kafka://` URLs: produce, consume from an offset, as the extension `Curl.Extension.Kafka.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 20 of 63. Specification: Apache Kafka protocol guide. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Kafka.UnitLibrary` and `Curl.Extension.Kafka.UnitTests` exist in `/Extensions/Kafka/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `kafka://` URLs: produce, consume from an offset.
- [x] Unit tests check its wire behaviour against named sections of Apache Kafka protocol guide, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Touches widened to `ExtensionDescriptionCatalogue.cs` (the description line `curl --extensions` reads) and `Curl.Console.UnitTests/CurlCompositionTests.cs` (its list of connecting handlers counts every handler); no task in Doing on origin/work/dark-factory names either.
- Decisions in ADR-1076: `kafka`/`kafkas` on 9092/9093, `/topic[/partition][?offset=N]`, Produce v3 and Fetch v4 (the oldest Kafka 4.0 accepts, non-flexible), one record per produce, consume to the high watermark with a line feed after each value, gzip read, hand-written CRC-32C.
- Left for follow-ups: BL-4277 (snappy, lz4, zstd), BL-4275 (Metadata leader routing), BL-4276 (SASL PLAIN, after BL-4275).
- Measured: Measure-CodeQuality.ps1 gives Curl.Extension.Kafka.UnitLibrary 100% line and branch coverage, 78 members, none failing, worst CRAP 8; Curl.Extension.Composition.UnitLibrary 100/100, none failing. Full build clean, fast tests green. `curl --extensions` lists kafka, `--ai-help extensions` names it, and with `CURL_EXTENSIONS=0` `kafka://` gives `curl: (1) Protocol "kafka" not supported`.
- CI on Linux and macOS: the tests use no paths, OS error text or certificates and start no concurrent work, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9d093dda 7e52bf0e 66b7cc63 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. kafka:// and kafkas:// produce one record with -d and consume a partition from ?offset=N to its high watermark (ADR-1076); gates met, fast tests green
