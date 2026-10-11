---
id: BL-2097
title: Add upstream test258 to PassingUpstreamCases.txt
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2085]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2097 — Add upstream test258 to PassingUpstreamCases.txt

## Goal

Upstream test258 is on the conformance ratchet, so a regression in Proxy-Connection: close handling fails the build.

## Context

BL-2085 made `HttpConnectionPersistence.KeepsAlive` honour `Proxy-Connection: close` on an HTTP/1.1 response through an HTTP proxy, which upstream test258 (curl 8.21.0, item `behaviour:test258` of GF-0005) needs. BL-2085 could not edit `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` because BL-2093 held that project on another lane. Run test258 in process; if it passes add 258 to the list, and if it still fails, file the remaining cause as a task.

## Acceptance criteria

- [x] `258` is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance ratchet passes, or a task names what still makes test258 fail.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Ran test258 in process through the ratchet: it passes with BL-2085's fix, so 258 is listed. No design decision needed.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. test258 passes; 258 added to PassingUpstreamCases.txt, conformance project green
