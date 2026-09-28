---
id: BL-445
title: Send -F @/dev/null chunked on Linux and macOS as libcurl sends a character device
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-402]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-445 — Send -F @/dev/null chunked on Linux and macOS as libcurl sends a character device

## Goal

`curl -F f=@/dev/null <url>` on Linux and macOS sends the same first request as the platform's curl 8.21.0 (OpenSSL build): chunked if libcurl's `S_ISREG` check sends a character device chunked, as `lib/mime.c` reads.

## Context

- Found in BL-401 (ADR-0097). libcurl 8.21.0's `curl_mime_filedata` gives a part the `stat` size only when `S_ISREG` holds, otherwise an unknown size (chunked). `PhysicalFileSystem` opens `/dev/null` seekable with length 0, so `MultipartFormBodyBuilder` declares `Content-Length` for it, and `UnseekableFileLength` is never asked.
- Needs a Linux or macOS measurement first (BL-402 measures the OpenSSL build); confirm the chunked body before changing anything. The fix would have the builder ask whether the path is a regular file (e.g. `File.GetUnixFileMode`/`FileSystemInfo` attributes or `stat` via the BCL) rather than whether its stream seeks.

## Acceptance criteria

- [x] The OpenSSL build's first request for `-F f=@/dev/null` is recorded in Notes with the command.
- [x] A `MultipartFormBodyBuilderTests` case pins our first request against it, marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` where it touches the real device.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- **Measured 2026-09-27** on WSL 2 Ubuntu, curl 8.18.0 libcurl/8.18.0 OpenSSL/3.5.5 (the
  same host and build BL-402 used; no 8.21.0 OpenSSL build is available). `nc` in WSL
  listened and answered 200 after 3 s so the whole body arrived; run by
  `wsl.exe -d Ubuntu -- bash bl445.sh`:
  ```
  ((sleep 3; printf 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok') | timeout 5 nc -l -p 18445 > /tmp/bl445.req) &
  sleep 1
  curl -sS -F f=@/dev/null http://127.0.0.1:18445/
  ```
  First request (CRLF line ends): `POST / HTTP/1.1`, `Host: 127.0.0.1:18445`,
  `User-Agent: curl/8.18.0`, `Accept: */*`, `Transfer-Encoding: chunked`,
  `Content-Type: multipart/form-data; boundary=------------------------vqgGmrr7xDndwoOS2J8VDy`,
  `Expect: 100-continue`, blank line, then one chunk `cd` (205 bytes):
  `--<B>` / `Content-Disposition: form-data; name="f"; filename="null"` /
  `Content-Type: application/octet-stream` / blank / blank / `--<B>--`, then `0` and the
  final blank line. Exit 0. Chunked, as ADR-0097 read `lib/mime.c`.
- **Decision (ADR-0104).** The BCL cannot report a file's type (`FileAttributes` has no
  device flag on Unix, `GetUnixFileMode` masks the type bits) and a `stat` P/Invoke would
  depend on per-platform struct layouts, so off Windows a seekable file under `/dev/`
  declares no length, except `/dev/shm/`, `/dev/fd/` and `/dev/std*`. New
  `Multipart\SeekableFileLength` beside `UnseekableFileLength`; the builder takes it as an
  optional last constructor argument, so `Curl.Console` is unchanged.
- **Touches widened** to `Documentation/Planning/Decisions` for ADR-0104; none of the tasks
  in Doing (BL-387, BL-459) names it.
- Tests: `MultipartFormBodyBuilderTests.TheNullDeviceDeclaresNoLengthOffWindowsAsCurlSendsItChunked`
  pins the measured body (205 bytes, no length) on every platform;
  `TheRealNullDeviceIsSentChunkedByDefaultOffWindows` opens the real device, excluded on
  Windows; `SeekableFileLengthTests` covers the path rule. Core: 915 passed, 6 skipped; Core
  coverage 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -F @/dev/null (any device under /dev/) is sent chunked on Linux and macOS as libcurl sends it (ADR-0104)
