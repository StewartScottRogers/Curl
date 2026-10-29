---
id: BL-4209
title: OAuth2 extension: keep tokens in an on-disk cache and refresh an expired one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4207, BL-4208]
touches: [Curl.Extension.OAuth2.UnitLibrary/Cache, Curl.Extension.OAuth2.UnitTests/Cache]
requirement: none
created: 2026-10-29
completed:
---
# BL-4209 — OAuth2 extension: keep tokens in an on-disk cache and refresh an expired one

## Goal

The OAuth2 extension keeps tokens in an on-disk cache keyed by token endpoint, client ID and scope, gives back an unexpired one, and refreshes an expired one with its refresh token (RFC 6749 §6).

## Context

Third part of BL-4161 (split for size). Expiry comes from `expires_in` (RFC 6749 §5.1) against the injected `TimeProvider`, with a small safety margin. The cache file lives in a directory the caller names (the default decided by ADR, beside curl's own per-user files), written whole and atomically, created with owner-only permissions where the OS has them. A refresh that fails with `invalid_grant` drops the entry so the flow runs again; a new refresh token in the response replaces the old (§6). Tests use a temporary directory they create and delete, and keep paths platform-neutral.

## Acceptance criteria

- [ ] Unit tests check storing, reading, expiry by a fake `TimeProvider`, refresh (RFC 6749 §6) and a corrupt cache file read as empty.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the cache location, format and key.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).

## Log

- 2026-10-29: Created.
