---
id: BL-253
title: Re-send a -T upload from its start when a followed redirect keeps PUT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-203]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-253 — Re-send a -T upload from its start when a followed redirect keeps PUT

## Goal

`RedirectFollower` re-sends a `-T` upload from its first byte on every hop that keeps PUT (301, 302, 307, 308), as curl 8.21.0 does.

## Context

- Filed from BL-203. `RedirectFollower` (Curl.Core.UnitLibrary/RedirectFollower.cs) passes the same `ITransferContext.Upload` stream to the next hop, which the first hop has already read to its end.
- Measured on curl 8.21.0 (mingw) with `Record-CurlExchange.ps1`: `curl -sS -L --max-redirs 1 -T up.txt http://127.0.0.1:18203/a` answered `301 Location: /next` sends `PUT /a` with `abc`, then `PUT /next` with `Content-Length: 3` and `abc` again. On 303 the upload is dropped and the second request is a GET (already done in BL-203).
- A non-seekable upload (stdin, `-T -`) cannot be rewound; measure what curl does for it before deciding.

## Acceptance criteria

- [x] A `RedirectFollowerTests` test shows the second hop of a 301, 302, 307 and 308 reading the upload's bytes from position 0.
- [x] The behaviour for a non-seekable upload is measured on curl 8.21.0, recorded in Notes and pinned in a test.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-26, curl 8.21.0 (mingw): `curl -sS -L --max-redirs 1 -T - http://127.0.0.1:18253/a` with `abc` on stdin, first answer `<status> Location: /next`. On 301, 302, 307 and 308: `PUT /a` chunked `3\r\nabc\r\n0\r\n\r\n`, then `PUT /next` chunked with an empty body `0\r\n\r\n`; exit 0, last body on stdout. On 303: `GET /next`, no body. So curl does not rewind or buffer stdin; the next hop sends what is left of it, which is nothing.
- Fix: `RedirectFollower` records a seekable upload's position before the first hop and restores it before each followed hop that keeps the upload (`SeekableStart`, `RewindUpload`); a non-seekable upload is passed on untouched. Recorded as ADR-0033.
- Rewinds to where the first hop started rather than to literal 0, so an upload handed over at an offset is re-sent from that same offset; for `-T file` that is 0. Sensible default, taken unattended.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0033; no task in Doing touches it.
- Tests: `FollowAsync_SeekableUploadRedirectKeepingPut_ResendsUploadFromItsStart` (301, 302, 307, 308; three hops each) and `FollowAsync_NonSeekableUploadRedirectKeepingPut_ResendsWhatIsLeftOfIt` (301, 307). The scripted handler now reads each hop's upload to its end, as a real handler does.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. `FollowChainAsync` first reached complexity 12; the seekable check moved into `SeekableStart` to bring it back to the limit.
- One full-solution test run inside the quality script failed once and passed on rerun (not in Curl.Core); not investigated here.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A followed PUT redirect re-sends a seekable -T upload from its start; stdin passes on as curl does (ADR-0033)
