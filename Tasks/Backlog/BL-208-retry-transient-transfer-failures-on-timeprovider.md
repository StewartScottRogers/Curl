---
id: BL-208
title: Retry transient transfer failures on TimeProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-160]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-208 — Retry transient transfer failures on TimeProvider

## Goal

A retry policy re-runs a transfer for curl's transient failures with curl's backoff and warning line, on the injected `TimeProvider`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Transient: timeout 28 and HTTP 408, 429, 500, 502, 503, 504; `Retry-After` honoured.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Which failures retry, the backoff sequence and `Retry-After` handling are measured on curl 8.21.0 and pinned on `FakeTimeProvider`.
- [ ] The warning `Warning: Problem : ... Will retry in N seconds. M retries left.` matches the measured text.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
