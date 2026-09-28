---
id: BL-402
title: Measure -L with a non-seekable -F file part on the OpenSSL build on Linux
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-402 — Measure -L with a non-seekable -F file part on the OpenSSL build on Linux

## Goal

On Linux, `curl -L -F f=@<fifo> <url>` answered 307 then 200 ends as curl 8.21.0's OpenSSL build on Linux ends it: the same exit code, stderr and `-w` values.

## Context

- BL-359 (2026-09-27) measured only the Schannel build on Windows, with a named pipe: exit 26, `curl: (26) read error getting mime data`, `%{http_code}` 307, `%{num_redirects}` 1, `%{url_effective}` the 307's target, `%{redirect_url}` empty. `RedirectFollower.StopBeforeHop` (`Curl.Core.UnitLibrary`) now returns that on every platform.
- On Linux `fseek` on a FIFO fails with `ESPIPE`, so libcurl may instead fail the rewind with exit 65 (`CURLE_SEND_FAIL_REWIND`) and a different message. Measure with a `mkfifo` file part on a Linux machine with curl 8.21.0 (OpenSSL), 307 then 200, with `-sS -w "[%{http_code}|%{num_redirects}|%{url_effective}|%{redirect_url}]"`.
- Needs a Linux host with curl 8.21.0; if none is available to the run, the task cannot be finished.

## Acceptance criteria

- [ ] The Linux measurement (command, exit code, stderr, `-w` output) is recorded in Notes.
- [ ] If it differs from the Windows one, `RedirectFollower` returns the Linux result when running on Linux, pinned in a `RedirectFollowerTests` case; if not, Notes say so.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
