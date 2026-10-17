# ADR-0641 — `CURLOPT_RESUME_FROM` skips a read-callback upload in the engine

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2473.

## Context

ADR-0609 (BL-2417) wired `CURLOPT_SEEKFUNCTION` for rewinds only. A libcurl handle that uploads through
`CURLOPT_READFUNCTION` with `CURLOPT_RESUME_FROM` set still sent its first bytes: the handlers skip a `-C`
offset only on a source that can seek (`HttpUploadResume`, `FtpUploadOffset`), and the engine's
`CallbackReadStream` cannot, so they sent it whole.

curl 8.22.0's `cr_in_resume_from` (`lib/sendf.c`, read from the release's source) calls the seek callback with
`(offset, SEEK_SET)`; on `CURL_SEEKFUNC_CANTSEEK`, or with no seek callback, it reads and discards `offset` bytes
through the read callback in 4 KiB pieces, a read of 0 or of more than it asked for (so `CURL_READFUNC_ABORT` and
`PAUSE` too) failing with `CURLE_READ_ERROR` and `Could only read N bytes from the input`, N counting that read's
return; any other seek result fails with `CURLE_READ_ERROR` and `Could not seek stream`; and when the upload's
size (`CURLOPT_INFILESIZE`) is known and the offset leaves none of it, `CURLE_PARTIAL_FILE` and
`File already completely uploaded`.

## Decision

- `Curl.Engine`'s `CallbackUploadResume` does exactly that over the read and seek callbacks, before the upload
  stream is made, so the discarded bytes do not count as read (`BytesReadSinceRewind`), as upstream's
  `read_len` does not count them.
- `ManagedEasyTransfer` calls it once per perform from the options, after the URL parses and the HSTS read
  callback runs and before the `CURLOPT_MIMEPOST` body is built, only for a handle that uploads through a read
  callback from a positive offset with no `ToolInputs` (the curl tool's runner resumes its own `-T` source). A
  failure ends the perform with its code and text before anything is set up or sent.
- The handlers still get the offset, so HTTP sends its `Content-Range` and FTP its `APPE`; the stream they read
  starts at the offset already.

## Consequences

A libcurl caller's resumed upload sends only the bytes past the offset, through its seek callback or by
discarding. Upstream does the skip after connecting, while the request is built; the engine does it before
connecting, so a failed skip opens no connection where libcurl would have opened one. Nothing the caller can
see in the codes or texts differs. This completes what ADR-0609's last paragraph left as not wired yet.
