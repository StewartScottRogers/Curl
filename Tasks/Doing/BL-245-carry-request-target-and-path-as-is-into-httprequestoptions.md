---
id: BL-245
title: Carry --request-target and --path-as-is into HttpRequestOptions in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-186, BL-191, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-245 — Carry --request-target and --path-as-is into HttpRequestOptions in Curl.Console

## Goal

`--request-target` and `--path-as-is` reach the HTTP handler from the command line.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W16. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Not in the plan's W list: BL-191 parses these and BL-186 honours them, but BL-236 covers only the transfer-encoding options.

## Acceptance criteria

- [ ] `curl --path-as-is http://h/a/../b` and `curl -X OPTIONS --request-target '*' http://h/` send BL-186's measured request lines over a fake connector.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W16 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
