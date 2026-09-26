---
id: BL-214
title: Apply --resolve and --connect-to overrides in the resolver and connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-202]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-214 — Apply --resolve and --connect-to overrides in the resolver and connector

## Goal

The resolver honours `--resolve` entries and the connector honours `--connect-to` mappings.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-202 parses them; BL-244 composes them in `Curl.Console`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Tests show an overridden host resolves to the given address and a `--connect-to` mapping changes the dialled host and port but not the Host header input.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
