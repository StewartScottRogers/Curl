# ADR-0416 — A file:// path decoding to NUL is refused with exit 3 before anything opens

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1451
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0's `file_connect` (`lib/file.c` lines 162-165) decodes the URL path with
`Curl_urldecode(..., REJECT_ZERO)`, which fails with `CURLE_URL_MALFORMAT` when an escape
decodes to a zero byte. No `failf` is written, so the tool prints `curl_easy_strerror`'s
bare text. Curl decoded every escape, NUL included, and handed the path to the file system:
a download failed with exit 37, an upload with exit 23.

Measured on Windows on 2026-10-07 with curl 8.21.0 (the Schannel build and the MinGW build
agree):

- `curl -sSv file:///dir/f.txt%00x` and `curl -sSv -T f.txt file:///dir/up%00x` each write
  only `curl: (3) URL using bad/illegal format or missing URL` - no `*` line under `-v`, no
  `shutting down connection` - and create nothing.
- `curl -sSv file:///dir/f%0atxt` is not refused: `* Could not open file /dir/f%0Atxt`, exit 37.
- With a drive letter, `curl -sSv file:///C:/dir/f.txt%00x` (download) instead writes
  `* URL rejected: Malformed input to a URL function` and exits 3: the URL parser's
  drive-letter handling refuses it before `file_connect` runs. An upload to a drive-letter
  path still gives the `file_connect` text.

## Decision

1. `FileProtocolHandler` refuses a parsed path whose `OsPath` holds a NUL with
   `CurlExitCode.UrlMalformat` and `URL using bad/illegal format or missing URL`, before the
   resume check and before any `IFileSystem` call, and reports no information line, for a
   download and an upload alike.
2. Only NUL is refused; every other decoded control character still reaches the file system.
3. The drive-letter download's different text belongs to URL parsing (`CurlUrl`, in
   `Curl.Protocol.Abstractions.UnitLibrary`) and is filed as its own task rather than
   imitated in the handler.

## Consequences

The drive-less form, which behaves the same on every platform, matches curl byte for byte.
Until the follow-up lands, a Windows drive-letter download with `%00` reports the
`file_connect` text instead of the URL parser's.
