# ADR-0603 — The header API records the engine's header output: `curl_easy_header` and `curl_easy_nextheader`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2446.

## Context

ADR-0512 put libcurl's header API on the seam as `ITransferHeaders` (`TryGet`, `Next`, `CurlHeader`,
`CurlHeaderOrigin`, `CurlHeaderCode`), but `ManagedEasyTransfer.Headers` threw `NotSupportedException`
and `Libcurl` had no `curl_easy_header` or `curl_easy_nextheader`. Upstream (curl 8.22.0,
`lib/headers.c`) keeps the headers through a client writer, `hds_cw_collect_write`, that sees every
write marked `CLIENTWRITE_HEADER` but not `CLIENTWRITE_STATUS`, and tags each with `CURLH_CONNECT`,
`CURLH_1XX`, `CURLH_TRAILER` or `CURLH_HEADER` from the write's flags; `http2.c` and the HTTP/3 code push
`:status` as `CURLH_PSEUDO`. No upstream source or real curl was reachable from the lane, so the rules
below follow upstream's code as written, not a measurement.

## Decision

1. **The engine records what its header output receives.** A perform from the options of an `http`,
   `https`, `ws` or `wss` URL writes its header lines through `HeaderRecordingStream`, which hands each
   complete line (Latin-1, line ending dropped) to the handle's `TransferHeaderStore` and the whole write
   to the header callback, when one is set. Other schemes record nothing: their header lines (FTP's
   server replies and the like) are not `name: value` headers, and libcurl keeps HTTP headers only.
   The curl tool's prepared attempt (`ToolAttempt`) and its `ToolOutputs` record nothing: the tool
   never reads the header API.
2. **Requests are numbered from the status lines.** Each status line that follows a final response's
   head (not 1xx, not CONNECT) starts the next request, so every redirect hop and authentication
   retry is its own request, as each is a new request in libcurl. A 1xx head and the final head after
   it share a request; so does a CONNECT reply and the response through its tunnel.
3. **Origins come from the head a line is in.** Lines under a 1xx status line are `CURLH_1XX`; lines
   under a CONNECT reply are `CURLH_CONNECT`, known because `ConnectReplyHeadWritingEvents` marks the
   output (`IConnectReplyHeadMarking`, passed through `HeaderLineTeeStream` to the `-D` side) just
   before it writes the reply head; lines after a final head has ended are `CURLH_TRAILER`; the rest
   are `CURLH_HEADER`. An `HTTP/2` or `HTTP/3` status line also records `:status` with its code as
   `CURLH_PSEUDO`.
4. **Lines are stored as `headers.c` stores them.** The name is as the server cased it, the value
   without leading or trailing blanks; a folded line (starting with a blank) is appended to the header
   before it keeping one leading blank and no trailing ones (`unfold_value`); a line with no colon, or
   a fold with no header before it, is dropped rather than failing the transfer.
5. **`TryGet` and `Next` are `curl_easy_header` and `curl_easy_nextheader`.** `TryGet` checks in
   upstream's order: a negative index (upstream's is a `size_t`), no origin bit, a bit `header.h` does
   not define or a request below -1 is `CURLHE_BAD_ARGUMENT`; no headers at all `CURLHE_NOHEADERS`; a
   request past the last `CURLHE_NOREQUEST`; then no header of that name, origin and request
   `CURLHE_MISSING`, and an index past them `CURLHE_BADINDEX`. Names match ignoring case; `amount` and
   `index` count the headers of that name within the origin mask and request. `Next` answers `null` for
   a request past the last, and finds where it left off from the `CurlHeader` it answered before, kept
   by reference as libcurl keeps the `anchor`; a `CurlHeader` it did not answer, or one from before the
   headers were forgotten, answers `null`.
6. **Headers last until the next perform.** Every perform and `Reset` forget them; a duplicate starts
   with none.
7. **`Libcurl`'s `CurlEasyHeaders` passes through.** `Header` (`curl_easy_header`) and `NextHeader`
   (`curl_easy_nextheader`) are `CurlEasy` extension methods, like the WebSocket API (ADR-0585): a
   `null` or cleaned-up handle, or a `null` name, answers `CURLHE_BAD_ARGUMENT` (`NextHeader` `null`)
   without the engine; every other check is the engine's.

## Consequences

- A CONNECT reply's headers are tagged only when the reply comes through `ConnectReplyHeadWritingEvents`,
  which needs a header output and no `CURLOPT_SUPPRESS_CONNECT_HEADERS`; with that option set libcurl
  still stores them, Curl does not.
- Recording costs one line buffer per transfer whether or not the application reads the headers.
