---
id: BL-1333
title: End an FTP download whose -r text names no range after EPSV with exit 0, as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1333 — End an FTP download whose -r text names no range after EPSV with exit 0, as curl 8.21.0 does

## Goal

When an `ftp://` download is handed `-r` text that names no range (`ITransferContext.RangeText` set, `ITransferContext.Range` `null`), `FtpSession` logs in, sends `PWD` and `EPSV`, opens the data connection, then ends with exit 0: no `TYPE`, `SIZE` or `RETR`, nothing written to the output.

## Context

- Today `Curl.Console` refuses such text before any handler runs (`CurlCommandRunner.TryParseRange` returns `false` for every scheme but `http`/`https`, giving `ByteRangeParser.NotDeliveredFailure`, exit 33), so `FtpSession` never sees it. BL-1322 changes the console to hand the text to the FTP handler instead; this task makes the handler ready for it first. `FtpDownloadWindow.Of(ITransferContext)` reads only `context.Range`, so today a `null` range would download the whole file.
- curl 8.21.0 (tag `curl-8_21_0`) parses FTP's range late: `lib/ftp.c` `ftp_do_more`, line 2327, calls `Curl_range(data)` only once the data connection is set up, and `lib/curl_range.c` lines 35-89 return `CURLE_RANGE_ERROR` for text with no dash (`abc`), a `-0` suffix, or a last position before the first (`5-2`). After that the download sends no `TYPE`/`RETR`, and the measured exit is 0: the error does not reach the exit code.
- Measured 2026-10-03 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Ftp -CurlArgs '-sv','-r','5-2','ftp://127.0.0.1:PORT/f.txt'`: exit 0, stdout empty; the transcript is `220`, `USER anonymous`, `331`, `PASS ftp@example.com`, `230`, `PWD`, `257 "/"`, `EPSV`, `229 (|||port|)`, then `QUIT` / `221` as the connection is closed at exit. stderr after the `229` line: `* Connect data stream passively`, `* Connecting to 127.0.0.1 port <p>`, `*   Trying 127.0.0.1:<p>...`, `* Established 2nd connection to ...`, `* Remembering we are in directory ""`, `* Connection #0 to host 127.0.0.1:PORT left intact`. `-r abc` and `-r -0` measured the same.
- The handler needs no parser of its own: `Range == null && RangeText != null` is exactly "the text names no range" for a non-HTTP scheme (see `ITransferContext.Range`'s remark). An upload (`-T`) ignores `-r` as today.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ftp.UnitTests` runs a download with `RangeText = "5-2"` and `Range = null` against the fake control connection and asserts the commands sent are exactly `USER`, `PASS`, `PWD`, `EPSV`, with the data connection opened and no `TYPE`, `SIZE` or `RETR`; the result is exit 0 (`CurlExitCode.Ok`), 0 bytes, nothing written to the output.
- [x] The same test asserts the `-v` info lines end with `Remembering we are in directory ""` as on a normal download that sends `PWD`, and that no `Requested range was not delivered` text is reported.
- [x] A data-driven test pins the same for `abc` and `-0`.
- [x] A test pins that `RangeText = "2-3"` with `Range` bounded 2-3 still downloads the window as today, and that `RangeText = null` downloads the whole file.
- [x] `dotnet build Curl.Protocol.Ftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports no failing member.

## Notes

- `FtpSession.OpenDownloadDataConnectionAsync` opens the data connection, then
  `EndWhenRangeTextNamesNoRangeAsync` ends the download when `Range` is `null` and
  `RangeText` is set: the `Remembering we are in directory` line, `QUIT`, exit 0 and the
  `left intact` line, as a normal download ends. A wrapper rather than one more `??` in
  `DownloadAsync`, which would have reached a cyclomatic complexity of 11.
- A listing ends the same way: measured 2026-10-03, `curl -sv -r 5-2 ftp://127.0.0.1:port/`
  sent `USER`, `PASS`, `PWD`, `EPSV`, `QUIT`, exit 0, the same stderr as for a file. Pinned
  by `ExecuteAsync_ListingWithRangeTextNamingNoRange_EndsBeforeList`.
- Tests: `FtpProtocolHandlerUnparsedRangeTests` (601 FTP tests pass).
- The coverage criterion found `FtpStateTrace.StateOf` failing (branch 71%, complexity 122,
  from BL-1199's string `switch`, which compiles to a branch per character tested). It is
  inside this task's `touches`, so it was rewritten as a dictionary and two set lookups,
  same states; lane 1 measured the library at 100% line, 100% branch, 0 failing members,
  worst CRAP 10.
- Lane 7 (second run) cherry-picked lane 1's work unchanged. Lane 1's integration failed on
  `Curl.Conformance.UnitTests: test1677`, an HTTP case served with `writedelay: 500` that
  this FTP-only change cannot reach; here the full fast suite passed with test1677 green.
  The load-dependent failure is filed as BL-1355.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Backlog. Lane 1 could not integrate: fast tests failed twice (Curl.Conformance.UnitTests: test1677; then Curl.Conformance.UnitTests: test1677) after rebasing onto the other lanes' work. The work is on branch factory/BL-1333-lane-1-20261003-061205; start with git cherry-pick --no-commit factory/BL-1333-lane-1-20261003-061205 and fix it.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An ftp:// download or listing whose -r text names no range ends after EPSV with exit 0, as curl 8.21.0 does
