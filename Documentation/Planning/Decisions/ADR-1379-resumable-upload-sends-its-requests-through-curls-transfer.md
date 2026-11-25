# ADR-1379: Resumable upload sends its requests through curl's transfer

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation (BL-5869, fitness finding EXT-119)

## Context

`--resumable-upload` (ADR-1103) sent its `OPTIONS`, creation, `HEAD` and `PATCH` requests over curl's
connector with its own HTTP/1.1 client (ADR-1095) and never called the next hook. So `--har`, `--record`,
`-v`, `--trace` and `--trace-ascii` saw nothing, and every header option (`-A`, `-e`, `-u`, `-b`,
`--oauth2-bearer`) had to be copied by hand into `ResumableUploadRequestHeaders`, one fitness finding at a time
(EXT-103, EXT-104, EXT-115, EXT-116). `--extended-help` says `--har` records each HTTP request and response.

## Decision

`ResumableUploadSession` sends each request by calling `next` with a `ResumableUploadRequestContext`: the
transfer's own context with the request's URL, method (`HEAD` as `-I` sends it), the flow's fields followed by
the `-H` lines that name neither a flow field nor `Content-Length`, the slice of the upload from the offset as
the `-T` body, `-f` off, no `-C`, `-r`, `-D` or `--data`, and the response body and head written to buffers.
The later hooks and curl's handler do everything else - authentication, TLS, proxy, `User-Agent`, tracing,
HAR and recording - exactly as for any other request. A request to another origin than the transfer's gets no
`Authorization`, `Cookie` or `Proxy-Authorization` from `-H`, no `-u`, `--oauth2-bearer` or `-b`, as curl
drops them on a cross-origin redirect. A result of 52, 55 or 56 (nothing received, send or receive broke)
can be resumed; any other failure ends the upload with its own exit code and message. The last response's
head, as the handler wrote it, goes to `-i` and `-D`, and its body to the output, after `-f` is applied.

The copied HTTP/1.1 client of ADR-1095, `ResumableUploadInterruptibleConnection` and
`ResumableUploadRequestHeaders` are deleted; the hook no longer needs `TransferHookServices`.

## Consequences

- `--har` records the `OPTIONS`, creation and append exchanges, `--record` stores each, and `-v` and
  `--trace-ascii` show them (EXT-119).
- The extension no longer refuses `--digest`, `--ntlm`, `--aws-sigv4` and the other authentication options:
  it hands them to curl's handler with each request.
- ADR-1095's wire-format client is superseded for this extension; its field types stay.
