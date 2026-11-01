# ADR-0646 — `CURLOPT_TRAILERFUNCTION` sends a chunked upload's trailers through `IUploadTrailerSource`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2475.

## Context

ADR-0611 (BL-2419) wired the prerequest and resolver-start callbacks and left `CURLOPT_TRAILERFUNCTION`
answering `NotBuiltIn`: `Curl.Original.Protocol.Http`'s `HttpRequestBodyWriter` wrote a fixed `0\r\n\r\n` closing chunk
and had no way to ask anyone for trailers.

curl 8.22.0's `add_last_chunk` (`lib/http_chunks.c`, read from the release's source; the curl tool has no option
that sets a trailer callback, so it cannot be measured from the command line) does this when the chunked reader
reaches the body's end:

- with no trailer callback, it adds `0\r\n\r\n` and traces `http_chunk, added last, empty chunk`;
- otherwise it adds `0\r\n`, calls the callback with an empty `curl_slist`, and on any answer but
  `CURL_TRAILERFUNC_OK` fails with `CURLE_ABORTED_BY_CALLBACK` and `operation aborted by trailing headers callback`;
- each line whose first `:` is followed by a space is added with its CRLF; any other is skipped with the info line
  `Malformatted trailing header, skipping trailer`; then the final CRLF, and the trace line
  `http_chunk, added last chunk with trailers from client -> 0`. A callback that adds nothing gives the same
  `0\r\n\r\n` bytes as no callback.

The chunked reader is installed only for an HTTP/1.x upload, so curl never calls the callback for HTTP/2 or
HTTP/3, which send no request trailers.

## Decision

- `Curl.Protocol.Abstractions` gets `IUploadTrailerSource`, beside `IUploadRewinder`: an upload stream says
  whether it has a trailer source (`SuppliesTrailers`) and gives its lines or aborts (`TryTakeTrailers`).
- `HttpRequestBodyWriter` makes the closing chunk once per send, when the body's end is first reached (in the
  read's trace or at the write, whichever comes first), from the body stream's source when it supplies trailers,
  as `add_last_chunk` does; an abort throws `HttpTransferException` with exit 42 and upstream's text, sending no
  closing chunk. The `--trace-config read` count of the last read includes the trailer bytes.
- `Curl.Engine`'s `CallbackReadStream` (and so `SeekingCallbackReadStream`) implements it over
  `EasyCallbackOptions.Trailer`; `ManagedEasyTransfer` sets it on the read-callback upload.
- `Libcurl`'s `Callbacks/Phases` sets `CURLOPT_TRAILERFUNCTION` (a `TrailerCallback` or `null`, else
  `BadFunctionArgument`) and `CURLOPT_TRAILERDATA` (any value, no change: the closure carries the user data).
- Trailer lines are encoded as UTF-8, as the managed strings come; upstream sends the bytes it is given.

## Consequences

A libcurl caller's chunked HTTP/1.1 read-callback upload sends the callback's trailers as curl does, and an abort
answers `CURLE_ABORTED_BY_CALLBACK` with upstream's text. HTTP/2 and HTTP/3 send none, as upstream, so no task is
left for them. The trace line upstream writes after an abort (`-> 42`) is not written; nothing else a caller can
see differs.
