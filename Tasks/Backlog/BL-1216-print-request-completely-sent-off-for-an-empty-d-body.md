---
id: BL-1216
title: Print Request completely sent off for an empty -d body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1216 — Print Request completely sent off for an empty -d body

## Goal

`curl -v -d '' <url>` prints `Request completely sent off`, as curl 8.21.0 does, not `upload completely sent off: 0 bytes`.

## Context

- Found in BL-1189, measured 2026-10-02: real curl 8.21.0 `-v --trace-config read -d ''` writes no reader lines and `Request completely sent off`; Curl writes `upload completely sent off: 0 bytes` (`HttpProtocolHandler.ReportRequestSent`).

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1` for `-d ''` and `-T` of an empty file; stderr in Notes.
- [ ] Tests pin each case's line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
