---
id: BL-361
title: Print the referer last sent under -e ;auto -L as %{referer}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-305]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-361 — Print the referer last sent under -e ;auto -L as %{referer}

## Goal

`-w "%{referer}"` prints the `Referer` the last request of a transfer was sent with, so under `-e "…;auto" -L` it is the URL the final redirect came from, as curl 8.21.0 prints it.

## Context

- BL-305 (ADR-0060) sets `TransferWriteOutVariables.Referer` from the `-e` value in `CurlCommandRunner.WriteOutAsync`; with `;auto` and `-L`, curl's `CURLINFO_REFERER` is the referer of the last request, which the runner does not know.
- The redirect follower (or the HTTP report) knows the sent `Referer`; carrying it needs a place to record it, likely a `TransferReport` member (ADR-0015 governs what the report carries).
- Measure first with curl 8.21.0 (mingw, Schannel): `-e ";auto" -L` and `-e "http://r/;auto" -L` through one and two redirects, and without `-L`.

## Acceptance criteria

- [x] The measured `%{referer}` for each case in Context is pinned in `CurlCommandRunnerWriteOutTests`, with the commands and bytes in this task's Notes.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

- 2026-09-27, lane 2: `CommandLineOptions.AutoReferer` is parsed (BL-251) but nothing reads it:
  `RedirectFollower` (`Curl.Core.UnitLibrary`) never sets a hop's `Referer`, so a followed hop
  sends none. Printing the sent referer needs the follower to send it first, so this task now
  touches `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests`. BL-385 (in Doing) touches both, so
  the task went back to Backlog until BL-385 is Done.
- Suggested shape: an `HttpRequestOptions.AutoReferer` flag (mapped from the command line); under
  it `RedirectFollower.NextHop` sets the hop's `Referer` to the previous hop's URL without
  user info or fragment; the merged `TransferReport` (or the follower's result) carries the last
  hop's `Referer`, and `CurlCommandRunner.WriteOutAsync` prints that instead of the `-e` text.
- Measured 2026-09-27 with curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`,
  port 18361; each hop answers `HTTP/1.1 302 Found\r\nLocation: /b` (then `/c`)
  `\r\nContent-Length: 0\r\n\r\n`, the last `HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n`.
  All exit 0; stdout is exactly the `%{referer}` text, no newline.

  | Command (`curl -s ... -w "%{referer}" <url>`) | Referer headers sent, by hop | stdout |
  | --- | --- | --- |
  | `-e ";auto" -L http://127.0.0.1:18361/a`, one redirect | none; `http://127.0.0.1:18361/a` | `http://127.0.0.1:18361/a` |
  | `-e ";auto" -L .../a`, two redirects (/a -> /b -> /c) | none; `.../a`; `.../b` | `http://127.0.0.1:18361/b` |
  | `-e "http://r/;auto" -L .../a`, one redirect | `http://r/`; `.../a` | `http://127.0.0.1:18361/a` |
  | `-e "http://r/;auto" -L .../a`, two redirects | `http://r/`; `.../a`; `.../b` | `http://127.0.0.1:18361/b` |
  | `-e "http://r/;auto" .../a` (no `-L`, 302) | `http://r/` | `http://r/` |
  | `-e ";auto" .../a` (no `-L`, 302) | none | empty |
  | `-e ";auto" -L http://u:p@127.0.0.1:18361/a?q=1#f`, one redirect | none; `http://127.0.0.1:18361/a?q=1` | `http://127.0.0.1:18361/a?q=1` |

  The auto referer drops user info and fragment and keeps the query, as upstream test 2081 shows.

- 2026-09-27, lane 3: delivered as suggested. `HttpRequestOptions.AutoReferer` (mapped from
  `CommandLineOptions.AutoReferer` in `HttpRequestOptionsMapping`); `RedirectFollower.HopHttp`
  sets each followed hop's `Referer` to the previous hop's URL as `scheme://host[:port]path[?query]`
  (port only when not the default); the merged report's new `TransferReport.Referer` is the
  `Referer` of the last dispatched request; `CurlCommandRunner.WriteOutVariables` (extracted from
  `WriteOutAsync` to keep its complexity under the gate) prints it, else the `-e` text. ADR-0101
  records it and amends ADR-0015, so `Documentation/Planning/Decisions` was added to `touches`
  (no task in Doing names it).
- Pinned: `CurlCommandRunnerWriteOutTests.RunAsync_AutoReferer_PrintsTheRefererTheLastRequestWasSentWith`
  (the six rows of the table above, asserting stdout and every hop's sent `Referer`) and
  `RunAsync_AutoRefererFromAUrlWithUserAndFragment_PrintsItWithoutThemButWithTheQuery`;
  `RedirectFollowerTests` gains three tests for the hop referers and the reported one.
- Defaults taken, unmeasured: a refused redirect reports the `Referer` of the last request actually
  sent; a URL typed with its default port (`http://h:80/`) gives an auto referer without the port.
- Gates: `dotnet build -warnaserror` clean; fast tests green (17 projects); `Measure-CodeQuality.ps1`
  100% line and branch for Curl.Protocol.Abstractions, Curl.Core and Curl.Output. Curl.Console's
  only failing members are pre-existing and untouched: `DiskWriteOutFileOpener.TryOpen` (BL-432)
  and `DumpHeaderOutputStream.WriteAsync` (filed as BL-455). `dotnet format --verify-no-changes`
  flags only pre-existing line endings in three files this task does not touch.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Needs Curl.Core.UnitLibrary/UnitTests (RedirectFollower must send the auto referer), which BL-385 in Doing touches; waits until they no longer overlap
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -w %{referer} prints the Referer the last request was sent with; -e ';auto' -L now sends the previous URL as each hop's Referer
