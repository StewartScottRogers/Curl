---
id: BL-5046
title: Report size_download and size_header for --cache-dir hits and revalidations
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpCache.UnitLibrary/HttpCacheTransferHook.cs, Curl.Extension.HttpCache.UnitLibrary/StoredResponseOutput.cs, Curl.Extension.HttpCache.UnitTests/HttpCacheTransferHookTests.cs, Documentation/Planning/Decisions/ADR-1069-cache-dir-is-a-private-rfc-9111-cache-for-plain-http-get-in-a-transfer-hook.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5046 — Report size_download and size_header for --cache-dir hits and revalidations

## Goal

A response served from `--cache-dir` (fresh hit or revalidated 304) reports the bytes written as `%{size_download}` and the stored response's header size as `%{size_header}`, as `--replay` already does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-038 (Low, Confirmed).

**Repro.** Run a server on 127.0.0.1:48000 with `/fresh` (200, `Cache-Control: max-age=600`, 25-byte body) and `/stale` (200, `Cache-Control: no-cache`, `ETag: "s1"`, 11-byte body; answers 304 to `If-None-Match: "s1"`). Run `curl -s --cache-dir cd9 http://127.0.0.1:48000/fresh -o NUL -w "%{http_code} %{size_download} %{size_header}\n"` three times, and `curl -s --cache-dir cd8 http://127.0.0.1:48000/stale -o NUL -w "%{http_code} %{size_download} %{size_header}\n"` twice.

**Expected.** ADR-1069: a cache hit writes the stored response and `-w` sees its status and header fields; the sizes of the bytes actually written are reported, as `--replay` does. `/fresh`: `200 25 178` on every run. `/stale`: `200 11 175` on the revalidated run.

**Actual.** `/fresh` prints `200 25 178`, then `200 0 0` twice. The revalidated `/stale` run prints `200 0 133` (the 304's header size). The bodies are written correctly.

**Where to start.** `Curl.Extension.HttpCache.UnitLibrary/HttpCacheTransferHook.cs`: `Answered` (the fresh-hit path, builds a `TransferReport` with no `DownloadSize`/`HeaderSize`) and the 304 path (about line 99, which keeps the 304's report). `Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs` lines 51 to 56 set `DownloadSize` and `HeaderSize` from the stored exchange; copy that approach (`StoredResponseOutput` holds the stored head).

## Acceptance criteria

- [x] `Curl.Extension.HttpCache.UnitTests/HttpCacheTransferHookTests.cs` has a test that a fresh hit returns a `TransferReport` with `DownloadSize` 25 and `HeaderSize` equal to the size of the stored head written (178 for the review's `/fresh` response), on the first hit and the second.
- [x] A test in the same class shows a revalidated 304 reports `DownloadSize` 11 and `HeaderSize` 175 (the stored response's head as written), not the 304's 133.
- [x] Regression: a cache miss that is stored still reports the origin's sizes (`200 25 178`), and the bodies written are unchanged byte for byte.
- [x] ADR-1069 (`Documentation/Planning/Decisions/ADR-1069-cache-dir-is-a-private-rfc-9111-cache-for-plain-http-get-in-a-transfer-hook.md`) states that `-w` size fields on a hit report the bytes written.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fast-test run of Curl.Extension.HttpCache.UnitTests green (99); Measure-CodeQuality for the library: 0 failing members. The hook now reads the written body and head lengths from StoredResponseOutput.WriteAsync. Platform-neutral.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests need no server: use a fake stored response. The 178 and 175 header sizes are the review's numbers for its server; in tests assert equality with the length of the head the hook writes, not a number tied to a real server's header set.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: cd5b7f63 eb9b5d13 d8cf5412 6b237200 a0b49a29.
- 2026-11-12: Doing -> Done. --cache-dir hits and revalidations report size_download and size_header of the bytes written
