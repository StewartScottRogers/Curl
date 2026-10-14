---
id: BL-2185
title: Gopher rejects a CR or LF in the decoded selector with exit 3 (upstream test1609)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-13
completed:
---
# BL-2185 — Gopher rejects a CR or LF in the decoded selector with exit 3 (upstream test1609)

## Goal

`gopher://host:port/1/sel%0d%0aINJECT/1609` fails with exit 3 (`CURLE_URL_MALFORMAT`) before anything is sent, as curl 8.22.0 does, so upstream test1609 passes.

## Context

Found by BL-2175 (GF-0038, curl 8.22.0 changed behaviour): with `%GOPHERPORT` routed to the harness's Gopher stand-in, test1609 runs and fails: Curl sends `/sel\r\n` to the server and exits 0, where curl 8.22.0 rejects a selector whose URL-decoded form holds a CR or LF with exit 3 and sends nothing (`<verify><protocol>` is empty). Start in `Curl.Protocol.Gopher.UnitLibrary`, where the selector is decoded and written. Run the case with `CURL_UPSTREAM_CASE=1609` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [ ] A Gopher URL whose decoded selector holds `\r` or `\n` fails with exit 3 and writes no byte to the connection, pinned by a unit test in `Curl.Protocol.Gopher.UnitTests` (ADR-0482).
- [ ] test1609 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
