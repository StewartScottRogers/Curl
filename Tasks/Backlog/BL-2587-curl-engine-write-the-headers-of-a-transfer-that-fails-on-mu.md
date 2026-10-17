---
id: BL-2587
title: Curl.Engine: write the headers of a transfer that fails on multiple Location headers (test580)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2587 — Curl.Engine: write the headers of a transfer that fails on multiple Location headers (test580)

## Goal

A Libcurl transfer with `CURLOPT_HEADER` that fails on a response with several `Location:` headers writes the response's header lines to the write callback before failing with `CURLE_WEIRD_SERVER_REPLY` (8), as libcurl 8.22.0 does.

## Context

Found by BL-2527 running test580 through the ported `lib507` (multi interface): the program's stdout is compared with `<reply><data>` and is empty, expected `HTTP/1.1 302 eat this!` first. Check what libcurl writes before the error and what exit code Curl gives.

## Acceptance criteria

- [ ] test580 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
