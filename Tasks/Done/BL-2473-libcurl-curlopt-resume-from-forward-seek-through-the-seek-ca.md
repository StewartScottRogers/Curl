---
id: BL-2473
title: Libcurl: CURLOPT_RESUME_FROM forward seek through the seek callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2417]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2473 — Libcurl: CURLOPT_RESUME_FROM forward seek through the seek callback

## Goal

A read-callback upload with `CURLOPT_RESUME_FROM` set skips its first bytes as curl 8.22.0's `cr_in_resume_from` does: the seek callback with `(offset, SEEK_SET)`, and on `CURL_SEEKFUNC_CANTSEEK` (or no seek callback) reading and discarding that many bytes through the read callback.

## Context

ADR-0609 (BL-2417) wired `CURLOPT_SEEKFUNCTION` for rewinds only, through `SeekingCallbackReadStream` in `Curl.Engine.UnitLibrary`. Upstream `lib/sendf.c` `cr_in_resume_from`: seek result `FAIL` fails with `CURLE_READ_ERROR`... measure the exact codes and texts (`Could not seek stream`, `failed to read data`, `File already completely uploaded`) in curl 8.22.0's source before pinning them. Find how the engine passes `EasyTransferOptions.ResumeFrom` to the handlers for an upload first.

## Acceptance criteria

- [x] Engine tests cover seek `Ok`, `CantSeek` with read-and-discard, and `Fail`, each with the bytes sent.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md` is kept true.

## Notes

- Upstream measured: curl 8.22.0 `lib/sendf.c` `cr_in_resume_from` (fetched from the `curl-8_22_0` tag). Texts: `Could not seek stream` (26) for any seek result but OK/CANTSEEK; `Could only read N bytes from the input` (26) when a 4 KiB discarding read gives 0 or more than asked (N includes that read's return, so ABORT/PAUSE show their magic values); `File already completely uploaded` (18) only when `CURLOPT_INFILESIZE` is known and the offset leaves none. The task's "failed to read data" text is not in that function.
- Design (ADR-0638): `CallbackUploadResume` in the engine skips the offset over the callbacks before the upload stream is built, so the discarded bytes do not count as read for a later rewind (upstream's `read_len` does not count them either). Only for a handle's own inputs; the curl tool's `ToolInputs` path is unchanged. The handlers still get `ResumeFrom` for `Content-Range` / `APPE`; they never skip a non-seekable stream themselves, so nothing is skipped twice.
- Choice: the skip runs before connecting, where libcurl runs it while building the request after connecting. Codes and texts are the same; the only difference is that a failed skip opens no connection. Recorded in ADR-0638.
- ADR-0609's last line ("forward seek ... not wired yet") is now stale, but that file is outside `touches`; ADR-0638 records that it supersedes it.
- Quality: first run had `Skip` at cx 18 and `PerformFromOptionsOverCachesAsync` at 14; split into small members and `PerformResumingUploadAsync`; re-measured 0 failing members, 100% line and branch.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 6db39b31 4272a6b9 c4aff572 aa1ad728.
- 2026-10-16: Doing -> Done. A libcurl read-callback upload with CURLOPT_RESUME_FROM skips its offset through the seek callback, or by reading and discarding, with libcurl's codes and texts
