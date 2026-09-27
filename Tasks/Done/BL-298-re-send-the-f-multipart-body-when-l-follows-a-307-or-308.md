---
id: BL-298
title: Re-send the -F multipart body when -L follows a 307 or 308
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-233]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-298 — Re-send the -F multipart body when -L follows a 307 or 308

## Goal

`curl -L -F a=b <url>` answered with a 307 or 308 sends the same multipart body again to the new location, byte for byte as curl 8.21.0 does.

## Context

- Found in BL-233 (2026-09-26). `RedirectFollower` (`Curl.Core.UnitLibrary`) rewinds only `ITransferContext.Upload`; an `HttpRequestOptions.Body` that is a `StreamBody` (the `-F` body `MultipartFormBodyBuilder` builds, whose files are streamed) is carried to the next hop already read to its end. 301/302/303 drop the POST body and are unaffected.
- Measure curl 8.21.0 (`/mingw64/bin/curl`) with `Record-CurlExchange.ps1 -Connections 2` answering 307 then 200, record the command and bytes in Notes, then pin them. Options: rebuild the body per hop, or rewind a seekable `StreamBody` as `RewindUpload` does.

## Acceptance criteria

- [x] With an injected boundary, `-L -F a=b -F f=@file` against a 307 then a 200 sends the measured bytes on both connections.
- [x] The same for 308.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Measured 2026-09-27 with curl 8.21.0 (`/mingw64/bin/curl.exe`, Schannel) through
  `Record-CurlExchange.ps1 -Port 18298 -Connections 2`, every connection answered
  `HTTP/1.1 307 Moved` (then again with 308), `Location: /next`, `Content-Length: 0`,
  `Connection: close`, running `curl -L --max-redirs 1 -F a=b -F f=@<dir>\file.txt
  http://127.0.0.1:18298/first` with `file.txt` holding `hello` (the second 307 ends it
  with exit 47, which does not affect the requests). Both requests are `POST` with
  `Content-Length: 297` and the identical multipart body, **boundary included**
  (`------------------------ugfPuRE0XGGB3ODF3hLOBy` for 307), `/first` then `/next`.
  Pinned in `CurlCommandRunnerFormTests.RunAsync_FormFollowed307Or308_SendsTheMeasuredMultipartBodyOnBothRequests`.
- Choice: rewind the built body rather than rebuild it per hop. Rebuilding would draw a
  new boundary; curl keeps the old one, so only rewinding matches. Not an ADR: the
  behaviour is measured, not chosen.
- `ConcatenatedReadStream` now seeks when every segment seeks (memory segments and
  files opened from disk always do), counting each segment from where it stood when the
  body was built. `RedirectFollower` records the `StreamBody` content's start and rewinds
  it wherever it rewinds a `-T` upload: every followed hop that keeps the body (307, 308,
  and 301/302/303 under `--post30x`).
- A body with a non-seekable file part (pipe, device) is still sent as what is left of
  it; curl's behaviour there is unmeasured, filed as BL-357.
- `Measure-CodeQuality.ps1`: Curl.Core.UnitLibrary 100% line and branch, 0 failing.
  Curl.Console production code is unchanged by this task (only its tests); its one
  failing member, `DiskWriteOutFileOpener.TryOpen`, is covered only by BL-280's
  Integration tests by design and predates this task.
- Pipeline run in-session (no subagents) for this small change: plan and measurement
  here, tests first per acceptance criterion, implementation, verify.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -L resends the -F multipart body, same boundary, after a 307 or 308, as curl 8.21.0 does
