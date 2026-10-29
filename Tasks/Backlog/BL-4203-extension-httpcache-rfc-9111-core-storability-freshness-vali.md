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
completed:
---
# BL-4203 — Extension HttpCache: RFC 9111 core - storability, freshness, validators and the on-disk entry

## Goal

`Curl.Extension.HttpCache.UnitLibrary` and its `.UnitTests` exist in `/Extensions/HttpCache/` in `Curl.slnx` with the RFC 9111 rules `--cache-dir` (BL-4133) needs, as pure code over a directory: whether a response may be stored, its freshness, the conditional request headers, and reading and writing one stored response on disk.

## Context

Split from BL-4133 for size (its Notes ask for wire format first, then the client flow, then the option). Extension rules: ADR-0999, CLAUDE.md "Extensions"; references only `Curl.Protocol.Abstractions` and `Curl.Extension.Abstractions` (`VerifyExtensionIsolation`). Use the `new-project` skill; add the single-line `<Folder Name="/Extensions/HttpCache/">` under the marker comment in `Curl.slnx`. Specification RFC 9111. No option, hook or catalogue line here: BL-4133 adds those. Inject `TimeProvider` for every age computation.

## Acceptance criteria

- [ ] `Curl.Extension.HttpCache.UnitLibrary` and `Curl.Extension.HttpCache.UnitTests` exist in `/Extensions/HttpCache/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] Storability (RFC 9111 §3: method GET, status, `no-store`, `private`, `Authorization` §3.5) is decided by one type; unit tests name each section.
- [ ] Freshness lifetime and current age (§4.2.1 `max-age`/`s-maxage`/`Expires`, §4.2.2 heuristic from `Last-Modified`, §4.2.3 `Age`/`Date`) and `no-cache`/`must-revalidate` are computed by one type with an injected `TimeProvider`; unit tests name each section.
- [ ] The conditional request headers (§4.3.1: `If-None-Match` from `ETag`, `If-Modified-Since` from `Last-Modified`) and the 304 header update (§4.3.4) are built by one type; unit tests.
- [ ] One stored response (status line, headers, body, request time, response time) is written to and read back from a file in a cache directory named by a key derived from the method and target URI (§2), with a temporary directory in the tests; a corrupt or truncated file reads as no entry.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
