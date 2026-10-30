---
id: BL-4132
title: Extension: Use Compression Dictionary Transport
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4302, BL-4300, BL-4301]
touches: [Curl.Extension.CompressionDictionary.UnitLibrary, Curl.Extension.CompressionDictionary.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4132 — Extension: Use Compression Dictionary Transport

## Goal

Curl provides an option that advertises and uses shared dictionaries (`dcb`, `dcz`) with a dictionary store, as the extension `Curl.Extension.CompressionDictionary.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 31 of 63. Specification: RFC 9842. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.CompressionDictionary.UnitLibrary` and `Curl.Extension.CompressionDictionary.UnitTests` exist in `/Extensions/CompressionDictionary/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option that advertises and uses shared dictionaries (`dcb`, `dcz`) with a dictionary store.
- [ ] Unit tests check its wire behaviour against named sections of RFC 9842, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

2026-10-29 (lane 11, first run, split before any code): RFC 9842 needs a Brotli decoder and a Zstandard decoder that both take a dictionary, and the .NET 10 BCL has neither (BrotliDecoder cannot attach a dictionary; no Zstandard at all), so both are hand-built. The transfer hook it needs exists (`ITransferHook`: request headers, response headers, body stream wrapping, as HttpCache uses). Split: BL-4302 scaffolds the projects with the store and the header and framing wire format, BL-4300 the dcz Zstandard decoder, BL-4301 the dcb Brotli decoder. What stays here: the option, the transfer hook (advertise `Available-Dictionary`, `Dictionary-ID` and `dcb`/`dcz` in `Accept-Encoding`, decode the response, store `Use-As-Dictionary` responses), the catalogue line, the ADR, `--extensions` and `--ai-help` text.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: dc5985e7 519fd519 03be6ab0 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4302 (store and wire format), BL-4300 (dcz Zstandard decoder) and BL-4301 (dcb Brotli decoder)
- 2026-10-30: Backlog -> Doing. Criteria at claim: dc5985e7 519fd519 03be6ab0 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
