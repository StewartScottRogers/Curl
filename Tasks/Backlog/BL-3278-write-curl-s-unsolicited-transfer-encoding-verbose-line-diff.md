---
id: BL-3278
title: Write curl's Unsolicited Transfer-Encoding verbose line (differential srv-te-gzip-chunked-v)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3278 — Write curl's Unsolicited Transfer-Encoding verbose line (differential srv-te-gzip-chunked-v)

## Goal

curl -sv --compressed against a response with Transfer-Encoding: gzip, chunked writes "* Unsolicited Transfer-Encoding (gzip) found" before closing, as curl 8.22.0 does.

## Context

BL-3233 differential case srv-te-gzip-chunked-v (-sv --compressed {U}/te-gzip-chunked): curl 8.22.0 writes "* Unsolicited Transfer-Encoding (gzip) found" after the status line and before "* closing connection #0"; Curl omits it. Check exit code and body too.

## Acceptance criteria

- [ ] Curl's stderr for srv-te-gzip-chunked-v matches curl 8.22.0's, pinned by a unit test.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.
