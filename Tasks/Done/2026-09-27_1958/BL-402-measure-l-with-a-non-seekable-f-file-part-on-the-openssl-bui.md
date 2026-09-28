---
id: BL-402
title: Measure -L with a non-seekable -F file part on the OpenSSL build on Linux
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-402 — Measure -L with a non-seekable -F file part on the OpenSSL build on Linux

## Goal

On Linux, `curl -L -F f=@<fifo> <url>` answered 307 then 200 ends as curl 8.21.0's OpenSSL build on Linux ends it: the same exit code, stderr and `-w` values.

## Context

- BL-359 (2026-09-27) measured only the Schannel build on Windows, with a named pipe: exit 26, `curl: (26) read error getting mime data`, `%{http_code}` 307, `%{num_redirects}` 1, `%{url_effective}` the 307's target, `%{redirect_url}` empty. `RedirectFollower.StopBeforeHop` (`Curl.Core.UnitLibrary`) now returns that on every platform.
- On Linux `fseek` on a FIFO fails with `ESPIPE`, so libcurl may instead fail the rewind with exit 65 (`CURLE_SEND_FAIL_REWIND`) and a different message. Measure with a `mkfifo` file part on a Linux machine with curl 8.21.0 (OpenSSL), 307 then 200, with `-sS -w "[%{http_code}|%{num_redirects}|%{url_effective}|%{redirect_url}]"`.
- Needs a Linux host with curl 8.21.0; if none is available to the run, the task cannot be finished.

## Acceptance criteria

- [x] The Linux measurement (command, exit code, stderr, `-w` output) is recorded in Notes.
- [x] If it differs from the Windows one, `RedirectFollower` returns the Linux result when running on Linux, pinned in a `RedirectFollowerTests` case; if not, Notes say so.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- **Linux host.** WSL 2 Ubuntu (kernel 6.6.87.2) has curl 8.18.0 (x86_64-pc-linux-gnu)
  libcurl/8.18.0 OpenSSL/3.5.5, package `8.18.0-1ubuntu2.4`; no 8.21.0 OpenSSL build was
  available. Same host and version the earlier Linux measurements used (ADR-0009). Decided
  to measure with it rather than block: the rewind path is long-standing libcurl code.
- **How.** WSL's NAT network cannot reach Windows' 127.0.0.1, and `Record-CurlExchange.ps1`
  binds only loopback. BL-436 (in Doing) touches that script, so it was not edited; a
  throwaway copy in `%TEMP%` with the listener on `IPAddress.Any` served the WSL gateway
  address 172.26.96.1. Worth a `-ListenAddress` parameter later.
- **Measured 2026-09-27.** Script run in WSL by `wsl.exe -d Ubuntu -- bash bl402.sh <url>`:
  ```
  d=$(mktemp -d); mkfifo "$d/f"; (printf hello > "$d/f") &
  curl -L -sS -F "f=@$d/f" -w "[%{http_code}|%{num_redirects}|%{url_effective}|%{redirect_url}]" "$1"
  ```
  Responses: `HTTP/1.1 307 Temporary Redirect` with `Location: http://172.26.96.1:18402/second`
  and `Content-Length: 0`, then `HTTP/1.1 200 OK` with `ok`. URL `http://172.26.96.1:18402/first`.
  - exit code: `65`
  - stderr: `curl: (65) Cannot rewind mime/post data`
  - stdout: `[307|1|http://172.26.96.1:18402/second|]`
  - only the first request was sent: chunked POST (`Transfer-Encoding: chunked`,
    `Expect: 100-continue`), body `hello` in one multipart part, as ADR-0097 says for Linux.
- **It differs from Windows** (exit 26, `read error getting mime data`); `-w` values are the
  same. `RedirectFollower` now takes `runsOnWindows` (default `OperatingSystem.IsWindows()`):
  off Windows it returns exit 65 `CurlExitCode.SendFailRewind`, `Cannot rewind mime/post data`
  (`RedirectFollower.CannotRewindMessage`). macOS is given the Linux answer, unmeasured.
  ADR-0098 records this.
- **Touches widened** to `Documentation/Planning/Decisions` for ADR-0098; neither task in
  Doing (BL-376, BL-436) names it.
- Tests: `FollowAsync_NonSeekableStreamBodyAnswered307Or308OnWindows_FailsTheNextHopWithReadError`,
  `..._OffWindows_FailsTheNextHopWithCannotRewind`, and one `[OSCondition]` test each for the
  default on and off Windows. Core: 892 passed, 5 skipped; Core coverage 100% line, 100% branch,
  0 failing members.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -L with an unrewindable -F body now fails as the platform's curl does: exit 65 'Cannot rewind mime/post data' off Windows, exit 26 on Windows (ADR-0098)
