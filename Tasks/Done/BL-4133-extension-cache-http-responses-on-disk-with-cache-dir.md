---
id: BL-4133
title: Extension: Cache HTTP responses on disk with --cache-dir
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4204, BL-4203]
touches: [Curl.Extension.HttpCache.UnitLibrary, Curl.Extension.HttpCache.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4133 — Extension: Cache HTTP responses on disk with --cache-dir

## Goal

Curl provides `--cache-dir <dir>`: store, reuse and revalidate with ETag and Last-Modified, as the extension `Curl.Extension.HttpCache.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 32 of 63. Specification: RFC 9111. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.HttpCache.UnitLibrary` and `Curl.Extension.HttpCache.UnitTests` exist in `/Extensions/HttpCache/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--cache-dir <dir>`: store, reuse and revalidate with ETag and Last-Modified.
- [x] Unit tests check its wire behaviour against named sections of RFC 9111, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 1, prerequisite pass before any edit): `--cache-dir` cannot be built yet. (1) `ITransferHook` (ADR-1007, ADR-1009) always runs curl's handler and flushes the body stream before `TransferFinished`, so a hook can neither serve a fresh stored response without the origin (RFC 9111 §4.2) nor send the stored response in place of a 304 (§4.3.4): filed BL-4204 for that seam in `Curl.Extension.Abstractions.UnitLibrary/Transfer`. (2) Split for size as these Notes ask: BL-4203 creates the projects and the RFC 9111 core (storability §3, freshness §4.2, conditional headers and 304 update §4.3, the on-disk entry, cache key §2); this task then adds the option, the hook over BL-4204's member, the registration, the ADR, the `--extensions`/`--ai-help` check and the quality gates. (3) Like `--sse` (BL-4110), the cache adds no scheme, so its registration lands in `ExtensionTransferHookCatalogue`, `ExtensionOptionCatalogue` and `ExtensionDescriptionCatalogue` (one-line shared lists in `Tasks/SharedListFiles.txt`, held by no task in Doing; added to touches); record in the ADR that `ExtensionCatalogue.cs` gets no line because it holds protocol handler factories.

- 2026-10-29 (lane 4): Done. `HttpCacheExtension` (option, description, hook activator), `HttpCacheTransferHook` over BL-4204's `ExecuteAsync` seam, `CacheableRequest` (plain GET of the whole resource only) and `StoredResponseOutput`; one line each in the three Composition catalogues. Decisions in ADR-1069: private cache, responses with `Vary` not stored, no stale-on-error, no unsafe-method invalidation, ranged/resumed/`-z`/HEAD transfers pass through, cache write I/O errors leave the transfer as it was. `Curl.slnx` already held the HttpCache folder from BL-4203, so it needed no change; `ExtensionCatalogue.cs` gets no line (protocol handler factories only). Measure-CodeQuality: HttpCache 100% line and branch, worst CRAP 10, 0 failing; Composition 100/100, 0 failing. Fast tests green, build clean. Tests are platform-neutral (temp directory created and deleted, no OS error text), so CI on Linux and macOS is covered by keeping them so.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c2489ee9 e75d06bf b9bb7e59 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4204 (transfer hook that can answer or replace a response, needed for RFC 9111 4.2 and 4.3.4) and BL-4203 (HttpCache projects and RFC 9111 core, split for size)
- 2026-10-29: Backlog -> Doing. Criteria at claim: c2489ee9 e75d06bf b9bb7e59 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --cache-dir <dir> stores, reuses and revalidates HTTP GET responses with ETag and Last-Modified (RFC 9111)
