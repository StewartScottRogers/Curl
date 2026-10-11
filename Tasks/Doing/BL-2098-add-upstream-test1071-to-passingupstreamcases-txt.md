---
id: BL-2098
title: Add upstream test1071 to PassingUpstreamCases.txt
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-2086]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2098 — Add upstream test1071 to PassingUpstreamCases.txt

## Goal

The conformance ratchet holds upstream test1071, so a regression of the HTTP/1.0 auth retry fails the build.

## Context

BL-2086 made the authentication retry after an HTTP/1.0 response go out as HTTP/1.0 (`HttpRequestPlan.DowngradedAfter` in Curl.Protocol.Http.UnitLibrary). Its criterion to add test1071 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` moved here because BL-2097 held Curl.Conformance.UnitTests in Doing at the time. Targeted curl version: 8.21.0.

## Acceptance criteria

- [ ] test1071 passes in process through the conformance runner and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
