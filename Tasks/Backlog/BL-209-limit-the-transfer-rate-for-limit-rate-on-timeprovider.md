---
id: BL-209
title: Limit the transfer rate for --limit-rate on TimeProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-209 — Limit the transfer rate for --limit-rate on TimeProvider

## Goal

A rate-limiting stream wrapper holds throughput at `--limit-rate` using the injected `TimeProvider`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/manpage.html#--limit-rate (curl 8.21.0).

## Acceptance criteria

- [ ] On `FakeTimeProvider`, a 10 KiB transfer at 1 KiB/s takes 10 simulated seconds within one read's tolerance.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
