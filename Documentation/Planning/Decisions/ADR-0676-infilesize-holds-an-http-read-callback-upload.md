# ADR-0676 — `CURLOPT_INFILESIZE` holds an HTTP read-callback upload, and `CURLOPT_HEADEROPT` picks the `CONNECT` headers

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2645.

## Context

Upstream cases 1525 to 1527 upload `Hello Cloud!\n` through an HTTP proxy tunnel with `CURLOPT_INFILESIZE` 13 and
a read callback that answers 13 on every call and never 0. The engine read `CURLOPT_INFILESIZE` only for
`CURLINFO_CONTENT_LENGTH_UPLOAD`: its `CallbackReadStream` could not seek, so the HTTP handler sent the upload
chunked and read the callback until it returned 0, which it never did, and each case ran past the runner's 20
seconds. curl 8.22.0's `cr_in_read` in `lib/sendf.c` asks the callback for no more than `total_len` minus what it
has read, ends at `total_len` without asking again, and the request goes with `Content-Length: 13`.

Case 1527 also showed that the engine stored `CURLOPT_HEADEROPT` and never read it: under `CURLHEADER_UNIFIED` with
no `CURLOPT_PROXYHEADER` its `CONNECT` sent no `User-Agent` or `Expect`, where libcurl's `http_proxy.c` sends the
`CURLOPT_HTTPHEADER` list to the proxy and ignores `CURLOPT_PROXYHEADER`.

## Decision

- `CallbackReadStream.DeclaredLength` holds a read-callback upload to `CURLOPT_INFILESIZE` when it is known (0 or
  more) and the URL is `http` or `https` (`ManagedEasyTransfer.DeclaredUploadLengthOf`). The stream then asks the
  callback for at most what is left, answers 0 at the end without asking, and seeks: `Length` is the declared
  length and `Position` the bytes read plus any forward seek, so the HTTP handler sends a `Content-Length`. A
  forward seek skips ahead (the bytes `CallbackUploadResume` already discarded for `CURLOPT_RESUME_FROM`), a move
  back to 0 rewinds through the seek callback, and a failed rewind or any other move back is an `IOException`
  (libcurl's `necessary data rewind wasn't possible` with no seek callback).
- Other schemes keep reading until the callback returns 0, as before: their handlers read `CanSeek` in their own
  ways (FTP's offset, IMAP's and SMB's sizes), and no case measured needs the change there yet; widening it is
  its own task once a case shows it.
- `EngineComposition.CreateProxyTunnelOptions` sends `CURLOPT_HTTPHEADER`'s lines in the `CONNECT` under
  `HeaderOption.Unified` and `CURLOPT_PROXYHEADER`'s under `HeaderOption.Separate` (`ConnectHeadersOf`), and a
  handle's forwarded proxy request carries no `CURLOPT_PROXYHEADER` lines under `Unified`
  (`ManagedEasyTransfer.TransferSettingsOf`). The curl tool never sets `Unified`, so its requests do not change.

## Consequences

Cases 1525, 1526 and 1527 pass, and with them 1576 and 3034, which also upload a sized read callback over HTTP;
all five hold the ratchet. A short read callback (0 before the declared length) still ends the upload where it
stops rather than with libcurl's `client read function EOF fail` error.
