---
id: BL-306
title: Send curl's Content-Range for -r with a request body over HTTP
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-178]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-306 — Send curl's Content-Range for -r with a request body over HTTP

## Goal

A request with a body and `-r` sends the `Content-Range` curl 8.21.0 sends, in its place in the head, instead of no range header at all.

## Context

- Found in BL-178. ADR-0044 decided that the HTTP handler sends no `Range` when the request has a body, and filed this task.
- Measured on curl 8.21.0 (BL-178 Notes, `order-post-range`): `curl -d x -r 0-9 -z "Sun, 06 Nov 1994 08:49:37 GMT" URL` sends `Content-Range: bytes 0-9/1` after `Host` and before `User-Agent`. The `/1` is the body length.
- `HttpRangeHeader.ValueFor` returns null for a request with a body; `HttpRequestHeadFormatter.Format` places `Range`.
- Measure the other `-r` forms with `-d` (`100-`, `-500`) and with `-X PUT` before pinning them, using `Record-CurlExchange.ps1`.

## Acceptance criteria

- [ ] `-d x -r 0-9` sends `Content-Range: bytes 0-9/1` in the measured position; each other `-r` form sends its measured value, with the command and bytes recorded in Notes.
- [ ] ADR-0044's "A request with a body sends no `Range`" bullet is updated to say what is sent now.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-09-26: Created.
