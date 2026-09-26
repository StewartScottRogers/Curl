---
id: BL-215
title: Pool connections and reuse them across requests and URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-164, BL-173]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-215 — Pool connections and reuse them across requests and URLs

## Goal

A connection pool implements the BL-164 ADR so a reusable connection serves the next request to the same key.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-164 ADR decides the hand-back and the pool key.

## Acceptance criteria

- [ ] Tests show reuse for the same key, no reuse across keys or after close, and the connection count the report needs.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
