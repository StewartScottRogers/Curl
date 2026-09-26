---
id: BL-253
title: Re-send a -T upload from its start when a followed redirect keeps PUT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-203]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-253 — Re-send a -T upload from its start when a followed redirect keeps PUT

## Goal

`RedirectFollower` re-sends a `-T` upload from its first byte on every hop that keeps PUT (301, 302, 307, 308), as curl 8.21.0 does.

## Context

- Filed from BL-203. `RedirectFollower` (Curl.Core.UnitLibrary/RedirectFollower.cs) passes the same `ITransferContext.Upload` stream to the next hop, which the first hop has already read to its end.
- Measured on curl 8.21.0 (mingw) with `Record-CurlExchange.ps1`: `curl -sS -L --max-redirs 1 -T up.txt http://127.0.0.1:18203/a` answered `301 Location: /next` sends `PUT /a` with `abc`, then `PUT /next` with `Content-Length: 3` and `abc` again. On 303 the upload is dropped and the second request is a GET (already done in BL-203).
- A non-seekable upload (stdin, `-T -`) cannot be rewound; measure what curl does for it before deciding.

## Acceptance criteria

- [ ] A `RedirectFollowerTests` test shows the second hop of a 301, 302, 307 and 308 reading the upload's bytes from position 0.
- [ ] The behaviour for a non-seekable upload is measured on curl 8.21.0, recorded in Notes and pinned in a test.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
