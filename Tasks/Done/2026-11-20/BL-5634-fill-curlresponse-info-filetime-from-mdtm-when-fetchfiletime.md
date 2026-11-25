---
id: BL-5634
title: Fill CurlResponse.Info.FileTime from MDTM when FetchFileTime is set on an ftp:// HEAD
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlClient.SendAsync.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5634 — Fill CurlResponse.Info.FileTime from MDTM when FetchFileTime is set on an ftp:// HEAD

## Goal

With `FetchFileTime = true`, a `Curl.Api` `HEAD` on an `ftp://` URL reports the file's time in `CurlResponse.Info.FileTime`, as the README says and as libcurl's `CURLINFO_FILETIME` does.

## Context

From Surly cookbook bug report `2026-11-20T1331-Curl-bug-report.md` (all 343 recipes re-verified against Curl `7d167fa10a` and Surl `9ad60d80c`), finding 1.

BL-5603 (Done, `d1addaf8c8`) added `CurlClientOptions.FetchFileTime`, which sets `CURLOPT_FILETIME` so an FTP `HEAD` sends `MDTM`. The cookbook found that `CurlResponse.Info.FileTime` stays -1 after such a `HEAD`, though the synthesized `Last-Modified` header does carry the time. So the time is read but not carried into the response's info. Find where `Info.FileTime` is filled (the transfer info's file time, `CURLINFO_FILETIME`) and make the FTP `MDTM` result reach it; check the HTTP `Last-Modified` case at the same time, since BL-5603 promised both.

## Acceptance criteria

- [x] Against an FTP test server, a `Curl.Api` `HEAD` with `FetchFileTime = true` reports `Info.FileTime` equal to the file's `MDTM` time (seconds since the epoch), pinned by a test.
- [x] The same option on an HTTP request reports `Info.FileTime` from `Last-Modified`, pinned by a test.
- [x] Without `FetchFileTime`, `Info.FileTime` stays -1 on both, as libcurl does.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Decision: the engine leaves FileTime -1 for FTP (no SourceLastWriteUnixSeconds), so CurlClient.SendAsync falls back to the synthesized Last-Modified header (RFC1123) when FetchFileTime is set and FileTime is -1. HTTP already filled it from the engine (kept if set). Pinned by CurlClientFileTimeTests. Quality run for Curl.Api: 0 failing members. Without FetchFileTime it stays -1.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1331, finding 1.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 97812ebe f83af33d f4cf65f3 3d836d5e.
- 2026-11-20: Doing -> Done. FileTime falls back to Last-Modified
