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
completed:
---
# BL-2473 — Libcurl: CURLOPT_RESUME_FROM forward seek through the seek callback

## Goal

A read-callback upload with `CURLOPT_RESUME_FROM` set skips its first bytes as curl 8.22.0's `cr_in_resume_from` does: the seek callback with `(offset, SEEK_SET)`, and on `CURL_SEEKFUNC_CANTSEEK` (or no seek callback) reading and discarding that many bytes through the read callback.

## Context

ADR-0609 (BL-2417) wired `CURLOPT_SEEKFUNCTION` for rewinds only, through `SeekingCallbackReadStream` in `Curl.Engine.UnitLibrary`. Upstream `lib/sendf.c` `cr_in_resume_from`: seek result `FAIL` fails with `CURLE_READ_ERROR`... measure the exact codes and texts (`Could not seek stream`, `failed to read data`, `File already completely uploaded`) in curl 8.22.0's source before pinning them. Find how the engine passes `EasyTransferOptions.ResumeFrom` to the handlers for an upload first.

## Acceptance criteria

- [ ] Engine tests cover seek `Ok`, `CantSeek` with read-and-discard, and `Fail`, each with the bytes sent.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] `Curl.Engine.UnitLibrary/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
