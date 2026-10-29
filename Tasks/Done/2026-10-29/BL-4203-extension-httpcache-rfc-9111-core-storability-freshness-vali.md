---
id: BL-4203
title: Extension HttpCache: RFC 9111 core - storability, freshness, validators and the on-disk entry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpCache.UnitLibrary, Curl.Extension.HttpCache.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4203 — Extension HttpCache: RFC 9111 core - storability, freshness, validators and the on-disk entry

## Goal

`Curl.Extension.HttpCache.UnitLibrary` and its `.UnitTests` exist in `/Extensions/HttpCache/` in `Curl.slnx` with the RFC 9111 rules `--cache-dir` (BL-4133) needs, as pure code over a directory: whether a response may be stored, its freshness, the conditional request headers, and reading and writing one stored response on disk.

## Context

Split from BL-4133 for size (its Notes ask for wire format first, then the client flow, then the option). Extension rules: ADR-0999, CLAUDE.md "Extensions"; references only `Curl.Protocol.Abstractions` and `Curl.Extension.Abstractions` (`VerifyExtensionIsolation`). Use the `new-project` skill; add the single-line `<Folder Name="/Extensions/HttpCache/">` under the marker comment in `Curl.slnx`. Specification RFC 9111. No option, hook or catalogue line here: BL-4133 adds those. Inject `TimeProvider` for every age computation.

## Acceptance criteria

- [x] `Curl.Extension.HttpCache.UnitLibrary` and `Curl.Extension.HttpCache.UnitTests` exist in `/Extensions/HttpCache/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [x] Storability (RFC 9111 §3: method GET, status, `no-store`, `private`, `Authorization` §3.5) is decided by one type; unit tests name each section.
- [x] Freshness lifetime and current age (§4.2.1 `max-age`/`s-maxage`/`Expires`, §4.2.2 heuristic from `Last-Modified`, §4.2.3 `Age`/`Date`) and `no-cache`/`must-revalidate` are computed by one type with an injected `TimeProvider`; unit tests name each section.
- [x] The conditional request headers (§4.3.1: `If-None-Match` from `ETag`, `If-Modified-Since` from `Last-Modified`) and the 304 header update (§4.3.4) are built by one type; unit tests.
- [x] One stored response (status line, headers, body, request time, response time) is written to and read back from a file in a cache directory named by a key derived from the method and target URI (§2), with a temporary directory in the tests; a corrupt or truncated file reads as no entry.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: pure RFC 9111 types in Curl.Extension.HttpCache (ResponseStorability, FreshnessCalculator, CacheValidation, CacheKey, CacheDirectory with StoredResponseEncoding); decisions in ADR-1020 (private cache by default with an isSharedCache flag for the shared-cache rules, GET only without 206/304, SHA-256 key file names, magic-line binary entry written by temp file and move). Pipeline run directly rather than through subagents to keep within the cost cap.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.HttpCache.UnitLibrary: 100% line, 100% branch, max complexity 10 after moving the status list to a FrozenSet and splitting IsStorable. 73 unit tests; solution build clean, fast tests green on Windows. The file tests use a temporary directory they delete (a unit test per CLAUDE.md) and paths built with Path.Combine, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8db68a28 272fb791 06ac59f7 ee0d6d31 b9e0db74 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.HttpCache decides storability, freshness and validation under RFC 9111 and stores one response per key file
