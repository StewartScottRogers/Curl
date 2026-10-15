---
id: BL-2265
title: Send Connection: Upgrade, HTTP2-Settings before Upgrade: h2c in the h2c upgrade request (test1801)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2265 — Send Connection: Upgrade, HTTP2-Settings before Upgrade: h2c in the h2c upgrade request (test1801)

## Goal

The h2c upgrade request orders its headers as upstream curl does, so upstream case 1801 passes and is listed.

## Context

Found by BL-2259 (2026-10-14), run through Curl on Linux: test1801 fails with "<verify><protocol> differs at byte 80 (line 5): expected \"Connection: Upgrade, HTTP2-Settings\r\n\", got \"Upgrade: h2c\r\n\"". Curl.Protocol.Http.UnitLibrary HttpRequestHeadFormatter.AppendH2cUpgrade writes Upgrade and HTTP2-Settings, and the Connection line is built later; the Connection line must come first in this request. Cases 1700, 1702 and 1800 already pass, so keep them green.

## Acceptance criteria

- [ ] The h2c upgrade request puts Connection before Upgrade and HTTP2-Settings, with a focused unit test in Curl.Protocol.Http.UnitTests (ADR-0482).
- [ ] 1801 is in PassingUpstreamCases.txt if it passes on Linux and macOS CI; otherwise its first difference is in Notes with a follow-up task.
## Notes

## Log

- 2026-10-14: Created.
