---
id: BL-244
title: Compose --resolve and --connect-to into the connectors in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-214, BL-202, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-244 — Compose --resolve and --connect-to into the connectors in Curl.Console

## Goal

`--resolve` and `--connect-to` from the command line reach the resolver and connector BL-214 extended.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W15. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Not in the plan's W list: BL-214 implements the overrides in `Curl.Networking` but nothing composed them. Composition is in `Curl.Console/CurlComposition.cs`.

## Acceptance criteria

- [ ] A Console test over `RecordingConnector` shows the overridden address and mapped host/port are used.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W15 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
