---
id: BL-3275
title: Show a bare 1xx response's headers with -i, as curl 8.22.0 does (differential status-100)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3275 — Show a bare 1xx response's headers with -i, as curl 8.22.0 does (differential status-100)

## Goal

curl -si on a URL answering only HTTP/1.1 100 with Content-Length writes the 100 header block to stdout, as curl 8.22.0 does.

## Context

BL-3233 differential case status-100 (-si {U}/status/100, route in Curl.Conformance.Differential.UnitLibrary/Http): real curl 8.22.0 writes "HTTP/1.1 100 Status\r\nDate: ...\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\n", Curl writes nothing. Measure real curl before pinning.

## Acceptance criteria

- [ ] Curl's stdout for the status-100 case matches curl 8.22.0's, pinned by a unit test in Curl.Protocol.Http.UnitTests.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.
