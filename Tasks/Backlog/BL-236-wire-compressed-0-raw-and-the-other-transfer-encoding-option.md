---
id: BL-236
title: Wire --compressed, -0, --raw and the other transfer-encoding options in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-177, BL-180, BL-191, BL-231, BL-308]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-236 — Wire --compressed, -0, --raw and the other transfer-encoding options in Curl.Console

## Goal

`--compressed`, `-0`, `--http1.1`, `--raw`, `--tr-encoding` and `--ignore-content-length` reach `HttpRequestOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-154 ADR fixes the Accept-Encoding value.

## Acceptance criteria

- [ ] A Console test per option shows the request bytes or output BL-177/BL-180 pinned.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- 2026-09-26: BL-180 left `--tr-encoding` to BL-308, which adds its `HttpRequestOptions` member; this task now waits on it too.

## Log

- 2026-09-26: Created.
