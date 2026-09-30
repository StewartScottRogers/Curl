# ADR-0279 — A refused HTTP/3 stream rewinds a seekable upload and fails an unseekable one with exit 65

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-885.

## Context

BL-834 sends a request again on a new QUIC connection when the server refuses its HTTP/3
stream with `H3_REQUEST_REJECTED` before any byte of the response, as curl 8.21.0's
`Curl_retry_request` does. It left out a `-T` upload (`StreamBody`): a refused upload failed
at once with exit 56 `Failure when receiving data from the peer`.

curl-8_21_0's source, read on 2026-09-30:

- `lib/transfer.c`, `Curl_retry_request`: after `REFUSED_STREAM, retrying a fresh connect` and
  `Connection died, retrying a fresh connect (retry count: N)` it calls
  `Curl_creader_set_rewind(data, TRUE)`. Nothing there depends on the request having a body.
- `lib/url.c`, `url_find_or_create_conn` calls `Curl_init_do`, whose `Curl_req_start` calls
  `Curl_client_start` (`lib/request.c`, `lib/sendf.c`), which rewinds every client reader
  before the new connection is connected.
- `lib/sendf.c`, `cr_in_rewind`: a reader whose read callback was never called has nothing to
  rewind; otherwise it calls the seek callback with offset 0, `SEEK_SET`, and on any non-zero
  answer fails with `failf(data, "seek callback returned error %d", err)` and
  `CURLE_SEND_FAIL_REWIND` (65). `Curl_client_start` then fails with
  `rewind of client reader '%s' failed: %d`, the reader being `cr-in`. The error buffer keeps
  the first `failf`.
- `src/tool_cb_see.c`, `tool_seek_cb`: the tool always sets it (`src/config2setopts.c`); an
  `lseek` that fails, as on stdin from a pipe, answers `CURL_SEEKFUNC_CANTSEEK`, which is 2.

No real server refuses an HTTP/3 stream on demand, so this was read from source, not measured.

## Decision

1. A refused `-T` upload from a seekable stream is sent again, like any other refused request,
   rewound to where the transfer began reading it (`HttpRequestBodyWriter.Rewound`), so a
   `-C` offset still holds and the upload goes whole.
2. A refused `-T` upload from a stream that cannot seek (stdin) takes the same retry lines,
   then fails before the new connection is opened: exit 65, message
   `seek callback returned error 2`, after the `-v` lines `seek callback returned error 2` and
   `rewind of client reader 'cr-in' failed: 65`. The plan carries this as
   `HttpRequestPlan.UploadCannotRewind`, checked at the top of `ConnectAndExchangeAsync`.
3. curl skips the rewind when its read callback was never called. On HTTP/3 the body is always
   written before the response is read, so the refusal comes after the read; the handler does
   not model the unread case.
4. Whether curl writes a `closing connection` line for the retry's unopened connection was
   not measurable; none is written.

## Consequences

- `-T file --http3` survives a refused stream, as curl does.
- The retries that are not about refused streams (`CanSendAgainOnFreshConnection`, a 401 or
  407 answered with a `StreamBody`) still never send a stream again; they are unchanged.
