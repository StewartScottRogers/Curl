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
completed: 2026-10-15
---
# BL-2265 — Send Connection: Upgrade, HTTP2-Settings before Upgrade: h2c in the h2c upgrade request (test1801)

## Goal

The h2c upgrade request orders its headers as upstream curl does, so upstream case 1801 passes and is listed.

## Context

Found by BL-2259 (2026-10-14), run through Curl on Linux: test1801 fails with "<verify><protocol> differs at byte 80 (line 5): expected \"Connection: Upgrade, HTTP2-Settings\r\n\", got \"Upgrade: h2c\r\n\"". Curl.Protocol.Http.UnitLibrary HttpRequestHeadFormatter.AppendH2cUpgrade writes Upgrade and HTTP2-Settings, and the Connection line is built later; the Connection line must come first in this request. Cases 1700, 1702 and 1800 already pass, so keep them green.

## Acceptance criteria

- [x] Resolved as not changeable (see Notes): moving Connection before Upgrade would break listed case 1800 and contradict real curl's measured head, so no code changed and no unit test was added.
- [x] 1801 is not listed; its differences are in Notes. No follow-up task: the case contradicts listed cases 1800 and 1704 on the same request, so no Curl output can pass both.
## Notes

- Reproduced on Linux (WSL Ubuntu, `CURL_UPSTREAM_CASE=1801`, Release, invariant globalization) with `Connection` moved before `Upgrade`: the order difference went away and the next difference is the `HTTP2-Settings` value, expected `AAMAAABkAARAAAAAAAIAAAAA` (INITIAL_WINDOW_SIZE 1073741824), got `AAMAAABkAAQAAQAAAAIAAAAA` (65536).
- Vendored case 1800 sends the same request (`GET /%TESTNUMBER --http2`, `http` server, no other option) and expects `Upgrade`, `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA`, then `Connection` last - exactly what Curl sends and what BL-716 measured from real curl. Case 1704 expects the same. 1801 expects the opposite order and a different window, so the three cannot all pass; 1800 and 1704's expectations match the measured curl, so 1801 is the stale one.
- Local real curl has no HTTP/2 (`--http2` is refused), so the head could not be re-measured here.
- Decision (Claude, under Stewart's delegation): keep Curl as is, revert the experiment (code and tests restored, tree clean), leave 1801 unlisted. Fast tests of Curl.Protocol.Http.UnitTests passed (2067) with the experiment's matching test changes before it was reverted.

## Log

- 2026-10-14: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Case 1801 contradicts listed cases 1800 and 1704 on the same request; Curl left as measured, findings in Notes
