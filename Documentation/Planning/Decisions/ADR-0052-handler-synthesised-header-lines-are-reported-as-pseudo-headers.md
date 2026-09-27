# ADR-0052 — Handler-synthesised header lines are reported as pseudo-headers

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; BL-285,
  2026-09-27).

## Context

A `file://` download produces three header lines, `Content-Length`, `Accept-ranges` and
`Last-Modified`. Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-26, the commands in
BL-285's Notes: `curl -s -o NUL -w "[%header{Content-Length}][%{num_headers}]" file:///...`
prints `[][3]`, with or without `-D`, under `-I`, under `-r`, and even when `-C` past the end
then fails with exit 36. An unmet `-z`, a missing file and an upload print `[0]`.

curl's `%{num_headers}` counts the header lines the tool's header callback receives, while
`%header{}` and `%{header_json}` read libcurl's header API (`curl_easy_header`), which only
the HTTP family fills. `lib/file.c` writes its lines as client header writes and never files
them in that API.

`TransferWriteOutVariables` derived both `num_headers` and `%header{}` from
`TransferReport.ResponseHeaders`, so the three lines could not go there without
`%header{Content-Length}` printing a value curl does not print.

## Decision

`TransferReport` gains `PseudoHeaders`, header lines a handler synthesised for the header
stream that curl's header API does not hold, empty by default.

- `%{num_headers}` is `ResponseHeaders.Count + PseudoHeaders.Count`.
- `%header{}` reads `ResponseHeaders` only, so a pseudo-header is never found.
- `FileProtocolHandler` reports its pseudo-headers, the same pairs it writes to
  `HeaderOutput`, once the time condition is met and the header block was produced, whether
  or not `HeaderOutput` is set and whatever the body does after. It reports none after an
  unmet `-z`, a failed open, an upload, or a header output that failed mid-block (exit 23;
  not measured, the conservative choice). Its report also carries the bytes transferred as
  `DownloadSize`, because a report replaces `TransferResult.BytesTransferred` as the source
  of `%{size_download}`.

## Consequences

- `file://` `%{num_headers}` matches curl; `%header{}` stays empty as curl's does.
- FTP's `-I` lines, which curl also writes outside the header API, have a home when an FTP
  handler lands.
- Renaming the pseudo-headers or disguising their keys to dodge `%header{}` was rejected:
  the report would then say something false about what the handler produced.
