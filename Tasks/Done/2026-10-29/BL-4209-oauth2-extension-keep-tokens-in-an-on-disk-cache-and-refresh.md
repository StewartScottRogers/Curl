---
id: BL-4209
title: OAuth2 extension: keep tokens in an on-disk cache and refresh an expired one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4212, BL-4213]
touches: [Curl.Extension.OAuth2.UnitLibrary/Cache, Curl.Extension.OAuth2.UnitTests/Cache, Curl.Extension.OAuth2.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4209 — OAuth2 extension: keep tokens in an on-disk cache and refresh an expired one

## Goal

The OAuth2 extension keeps tokens in an on-disk cache keyed by token endpoint, client ID and scope, gives back an unexpired one, and refreshes an expired one with its refresh token (RFC 6749 §6).

## Context

Third part of BL-4161 (split for size). Expiry comes from `expires_in` (RFC 6749 §5.1) against the injected `TimeProvider`, with a small safety margin. The cache file lives in a directory the caller names (the default decided by ADR, beside curl's own per-user files), written whole and atomically, created with owner-only permissions where the OS has them. A refresh that fails with `invalid_grant` drops the entry so the flow runs again; a new refresh token in the response replaces the old (§6). Tests use a temporary directory they create and delete, and keep paths platform-neutral.

## Acceptance criteria

- [x] Unit tests check storing, reading, expiry by a fake `TimeProvider`, refresh (RFC 6749 §6) and a corrupt cache file read as empty.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the cache location, format and key.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).
- Added `Curl.Extension.OAuth2.UnitLibrary/CLAUDE.md` to touches so the library document describes the new `Cache/` folder; no task in Doing on work/dark-factory named it.
- Plan (ADR-1034): `OAuth2TokenCache` (find, store, remove; refresh through `OAuth2HttpClient`), `OAuth2TokenCacheFile` (whole read, atomic temp-file-and-move write, `0600` off Windows), `OAuth2TokenCacheFormat` (versioned JSON by `Utf8JsonWriter`/`JsonDocument`), `OAuth2TokenCacheKey`, `OAuth2CachedToken`, `OAuth2TokenCacheLocation`.
- Defaults taken: 30-second expiry safety margin; a token with no `expires_in` never expires until removed; `expires_in` capped at 100 years; a refresh failure other than `invalid_grant` keeps the entry and gives no token; a failed write returns `false` rather than throwing, as a cache never fails a transfer.
- Owner-only mode follows `PhysicalFileSystem`'s seam (`UnsupportedOSPlatformGuard` field, Windows test expecting `PlatformNotSupportedException`), so both branches are covered on every platform; the `0600` check runs off Windows only.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.OAuth2.UnitLibrary` 100% line, 100% branch, max complexity 10, 0 failing members (first run found `RefreshAsync` at 12; response reading split into two helpers). Fast tests green across the solution; CI on Linux and macOS is covered by keeping tests platform-neutral (temporary directories, `OSCondition` for the mode checks).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a6c4aafc 4e3ca97b 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. The OAuth2 extension keeps tokens in an on-disk cache and refreshes an expired one (ADR-1034)
