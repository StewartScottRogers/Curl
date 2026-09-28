---
id: BL-445
title: Send -F @/dev/null chunked on Linux and macOS as libcurl sends a character device
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-402]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-445 — Send -F @/dev/null chunked on Linux and macOS as libcurl sends a character device

## Goal

`curl -F f=@/dev/null <url>` on Linux and macOS sends the same first request as the platform's curl 8.21.0 (OpenSSL build): chunked if libcurl's `S_ISREG` check sends a character device chunked, as `lib/mime.c` reads.

## Context

- Found in BL-401 (ADR-0097). libcurl 8.21.0's `curl_mime_filedata` gives a part the `stat` size only when `S_ISREG` holds, otherwise an unknown size (chunked). `PhysicalFileSystem` opens `/dev/null` seekable with length 0, so `MultipartFormBodyBuilder` declares `Content-Length` for it, and `UnseekableFileLength` is never asked.
- Needs a Linux or macOS measurement first (BL-402 measures the OpenSSL build); confirm the chunked body before changing anything. The fix would have the builder ask whether the path is a regular file (e.g. `File.GetUnixFileMode`/`FileSystemInfo` attributes or `stat` via the BCL) rather than whether its stream seeks.

## Acceptance criteria

- [ ] The OpenSSL build's first request for `-F f=@/dev/null` is recorded in Notes with the command.
- [ ] A `MultipartFormBodyBuilderTests` case pins our first request against it, marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` where it touches the real device.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-27: Created.
