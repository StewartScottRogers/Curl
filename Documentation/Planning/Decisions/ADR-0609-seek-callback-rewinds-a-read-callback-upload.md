# ADR-0609 — `CURLOPT_SEEKFUNCTION` rewinds a read-callback upload

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2417.

## Context

ADR-0592 (BL-2463) gave `Curl.Protocol.Abstractions` the `IUploadRewinder` seam: an upload stream that
cannot seek but can be taken back to its start, which `RedirectFollower` and the HTTP handler rewind
before a followed redirect, an authentication retry, a resend after a `417` or a refused-stream retry
resends the body, once some of it was read, failing with exit 65 and the rewinder's message otherwise.
The managed engine reads `CURLOPT_READFUNCTION` through `CallbackReadStream`, which cannot seek, and
`EasyCallbackOptions.Seek` was carried but never called. curl 8.22.0's `cr_in_rewind` calls the seek
callback with offset 0 and `SEEK_SET` when the read callback has given bytes (`read_len`), and fails any
non-zero return with `seek callback returned error <n>` and `CURLE_SEND_FAIL_REWIND`; `CURL_SEEKFUNC_CANTSEEK`'s
read-and-discard workaround is in `cr_in_resume_from`, for `CURLOPT_RESUME_FROM`'s forward seek only.

## Decision

- `CallbackReadStream` counts the bytes the read callback gives (`BytesReadSinceRewind`). A new
  `SeekingCallbackReadStream` derives from it and implements `IUploadRewinder`: `HasBeenRead` is a non-zero
  count, and `RewindToStart` calls the seek callback with `(0, SeekOrigin.Begin)`, clearing the count on
  `SeekResult.Ok` and answering `seek callback returned error 1` or `2` otherwise.
- `ManagedEasyTransfer` builds a `SeekingCallbackReadStream` only when `CURLOPT_SEEKFUNCTION` is set. With no
  seek callback the upload stays a plain `CallbackReadStream`, which is no `IUploadRewinder`, so its resend
  keeps today's behaviour; libcurl's own fallback there (`necessary data rewind was not possible`) is left
  for when it is measured, since the curl tool hands its uploads to the engine as read callbacks too.
- `Libcurl/Callbacks/Seek` sets `CURLOPT_SEEKFUNCTION` (a `SeekCallback` or `null`, any other value
  `BadFunctionArgument`) and takes any `CURLOPT_SEEKDATA`, changing nothing, as the other callback areas do
  (ADR-0573, ADR-0577).

## Consequences

A libcurl caller's upload is sent whole again after a `307`/`308` or an authentication retry when its seek
callback rewinds, and the transfer fails with exit 65 when it cannot. `CURLOPT_RESUME_FROM`'s forward seek
through the callback is not wired yet.
