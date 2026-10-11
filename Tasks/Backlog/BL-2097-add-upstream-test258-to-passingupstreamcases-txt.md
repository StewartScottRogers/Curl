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
completed:
---
# BL-2097 — Add upstream test258 to PassingUpstreamCases.txt

## Goal

Upstream test258 is on the conformance ratchet, so a regression in Proxy-Connection: close handling fails the build.

## Context

BL-2085 made `HttpConnectionPersistence.KeepsAlive` honour `Proxy-Connection: close` on an HTTP/1.1 response through an HTTP proxy, which upstream test258 (curl 8.21.0, item `behaviour:test258` of GF-0005) needs. BL-2085 could not edit `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` because BL-2093 held that project on another lane. Run test258 in process; if it passes add 258 to the list, and if it still fails, file the remaining cause as a task.

## Acceptance criteria

- [ ] `258` is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance ratchet passes, or a task names what still makes test258 fail.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-11: Created.
