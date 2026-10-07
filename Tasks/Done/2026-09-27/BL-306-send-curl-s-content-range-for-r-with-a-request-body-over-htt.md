---
id: BL-306
title: Send curl's Content-Range for -r with a request body over HTTP
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-178]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0044-http-honours-range-resume-time-condition-and-max-filesize.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
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

- [x] `-d x -r 0-9` sends `Content-Range: bytes 0-9/1` in the measured position; each other `-r` form sends its measured value, with the command and bytes recorded in Notes.
- [x] ADR-0044's "A request with a body sends no `Range`" bullet is updated to say what is sent now.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members.

## Notes

- Measured 2026-09-27 on curl 8.21.0 (mingw64, Schannel) with `Record-CurlExchange.ps1` against
  `http://127.0.0.1:<port>/a`. Every request below is `POST /a` unless marked, then `Host`, the
  line shown, `User-Agent: curl/8.21.0`, `Accept: */*`, `Content-Length`, `Content-Type:
  application/x-www-form-urlencoded`, blank line, body:
  - `-d x -r 0-9` -> `Content-Range: bytes 0-9/1`
  - `-d x -r 100-` -> `Content-Range: bytes 100-/1`
  - `-d x -r -500` -> `Content-Range: bytes -500/1`
  - `-d hello -r 0-9` -> `Content-Range: bytes 0-9/5`
  - `-X PUT -d x -r 0-9` -> `PUT`, `Content-Range: bytes 0-9/1`; `-X PUT -d hello -r 100-` -> `Content-Range: bytes 100-/5`
  - `-d x -r 0-9 -z "Sun, 06 Nov 1994 08:49:37 GMT"` -> `Content-Range: bytes 0-9/1`, then `If-Modified-Since` after `Accept`
  - `-d x -r 0-9 -H "Transfer-Encoding: chunked"` -> `Content-Range: bytes 0-9/1` (the length, not chunked)
  - `-T global.json -r 0-9` (87 bytes) -> `PUT`, `Content-Range: bytes 0-9/87`, `Content-Length: 87`, no Content-Type
  - `-T - -r 0-9` (standard input) -> `PUT`, `Content-Range: bytes 0-9/-1`, `Transfer-Encoding: chunked`, `Expect: 100-continue`
  - `-F a=b -r 0-9` -> no `Content-Range` and no `Range` (curl's multipart post sends neither)
  - `-d x -r 0-9 -H "Content-Range: foo"` -> curl's line left out; `Content-Range: foo` after `Accept`
  - `-X POST -r 0-9` (no body) -> `Range: bytes=0-9` (unchanged: no body, no Content-Range)
  - `-d x -r 0-9,20-29` -> `Content-Range: bytes 0-9,20-29/1` (verbatim text; filed as BL-386)
  - `-d x -C 5`, `-F a=b -C 3` -> exit 2, `cannot mix --continue-at with --data`/`--form`; `-d x -r 0-9 -C 5` -> exit 2, mutually exclusive. Nothing for the handler.
- Design: `HttpRequestFraming.ContentRange` now holds the value (a resumed `-T` upload's own,
  else `HttpRangeHeader.ContentRangeFor(range, length)` for a `-d` body or a `-T` upload), so
  the 417 resend keeps it; `HttpRequestFraming.Of` takes `ITransferContext.Range`. A `-F` form is
  told apart as the `StreamBody` on `HttpRequestOptions.Body` (only `-F` builds one; `-d` is a
  `BytesBody`). Unknown length formats as `-1`, as curl's `infilesize`.
- Added `Documentation/Planning/Decisions/ADR-0044-...md` to `touches`: the acceptance criteria
  require updating it, and no task in Doing names it.
- Quality: `Of` and `OfBody` measured complexity 12 at first; the branches moved into
  `DataRangeOf` and `ContentRangeOf`, and the library reports 0 failing members, 100% line and
  branch coverage.
- Tests: 894 in `Curl.Protocol.Http.UnitTests`, all fast tests pass.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -r with a -d body or -T upload sends curl's Content-Range: bytes R/L after Host; -F sends none
