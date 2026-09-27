---
id: BL-240
title: Wire scheme guessing, URL globbing and the IPFS rewrite in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-204, BL-207, BL-210, BL-230, BL-351]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-240 — Wire scheme guessing, URL globbing and the IPFS rewrite in Curl.Console

## Goal

URLs pass through BL-204, BL-207 and BL-210 before dispatch in `Curl.Console`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W11. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-230 added as a dependency beyond the plan so the split runner is in place.

## Acceptance criteria

- [ ] `curl example.com`, `curl 'http://h/[1-3]' -o '#1.txt'` and `curl ipfs://<cid>` behave as measured on curl 8.21.0, over fakes.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W11 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- BL-351 added as a dependency by BL-210 (2026-09-27): the IPFS rewrite needs the `--ipfs-gateway` value, which Curl.Cli does not parse yet.

## Log

- 2026-09-26: Created.
