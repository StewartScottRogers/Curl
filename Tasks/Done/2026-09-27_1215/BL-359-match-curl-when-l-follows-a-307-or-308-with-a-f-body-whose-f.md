---
id: BL-359
title: Match curl when -L follows a 307 or 308 with a -F body whose file cannot seek
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-298]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-359 — Match curl when -L follows a 307 or 308 with a -F body whose file cannot seek

## Goal

`curl -L -F f=@<pipe or device> <url>` answered with a 307 or 308 does on the second request what curl 8.21.0 does: the same bytes, or the same exit code and message if curl refuses to rewind.

## Context

- Found in BL-298 (2026-09-27). `ConcatenatedReadStream` (`Curl.Core.UnitLibrary/Multipart`) seeks only when every segment seeks, and `RedirectFollower` rewinds a `StreamBody` only then. A file part streamed from a non-seekable stream (a named pipe, a device) leaves the body non-seekable, so the next hop sends whatever is left of it, chunked - unmeasured against curl.
- curl may fail such a hop with exit 65 (`CURLE_SEND_FAIL_REWIND`, "necessary data rewind wasn't possible"); measure first with `Record-CurlExchange.ps1 -Connections 2` (307 then 200) and a file part read from a pipe, and record the command and bytes in Notes.

## Acceptance criteria

- [x] With an injected non-seekable file stream, `-L -F f=@<path>` against a 307 then a 200 produces curl 8.21.0's measured second request, or its measured exit code and stderr, pinned in a `RedirectFollowerTests` or `CurlCommandRunnerFormTests` case.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Measured 2026-09-27 with curl 8.21.0 (`/mingw64/bin/curl.exe`, Schannel). The file part was
  a named pipe served from a `Start-Job` running `System.IO.Pipes.NamedPipeServerStream('<name>',
  Out)` that wrote `hello`, flushed, waited 300 ms and closed. Command:
  `Record-CurlExchange.ps1 -Port 18380 -Connections 2 -Response 'HTTP/1.1 307 Moved\r\nLocation: /next\r\nContent-Length: 0\r\nConnection: close\r\n\r\n' -CurlArgs '-sS','-L','--max-redirs','1','-w','[%{http_code}|%{num_redirects}|%{url_effective}|%{redirect_url}]','-F','f=@\\.\pipe\<name>','http://127.0.0.1:18380/first'`.
  Result, the same over four runs: exit 26, stderr `curl: (26) read error getting mime data`,
  stdout `[307|1|http://127.0.0.1:18380/next|]`. The second request's headers and the part's
  opening boundary and headers reach the wire before the read fails.
- Decision: `RedirectFollower.StopBeforeHop` ends the chain before the next hop with exit 26 and
  that message whenever a kept `-F` `StreamBody` cannot seek, counting the redirect and emptying
  `%{redirect_url}`. We do not send the partial second request curl sends before failing: the
  acceptance criterion allows matching the exit code and stderr, the partial bytes go to a
  server that is about to see the connection fail, and producing them would need the HTTP
  handler (outside this task's `touches`). Not an ADR: the outcome is measured, not chosen.
- The check sits after the refusal and hop-proxy checks, as curl only rewinds when it sends the
  next request; `DropsBody` now runs before `StopBeforeHop` so a 301/302/303 that drops the body
  still follows (`FollowAsync_NonSeekableStreamBodyAnswered302_DropsItAndFollows`). Moving the
  check out of `FollowChainAsync` keeps its complexity at or under 10 (it measured 14 inline).
- Only the Windows build could be measured; Linux may fail the rewind with exit 65 instead.
  Filed BL-402 to measure it. Also found: curl's *first* request for a pipe file part declares
  `Content-Length` from `stat` (0 bytes for a pipe) and truncates the body to it, where ours is
  chunked. Filed BL-401.
- Pinned in `RedirectFollowerTests.FollowAsync_NonSeekableStreamBodyAnswered307Or308_FailsTheNextHopWithReadError`
  (307 and 308), replacing `FollowAsync_NonSeekableStreamBodyAnswered307_ResendsWhatIsLeftOfIt`.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing,
  worst CRAP 10. Pipeline run in-session (no subagents) for this one-method change.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -L after a 307 or 308 with a -F body that cannot seek fails with curl's exit 26, read error getting mime data
