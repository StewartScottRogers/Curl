# ADR-0627: curl_mime_data_cb and raw curl_mime_data parts in a CURLOPT_MIMEPOST

- Status: Accepted
- Date: 2026-10-16
- Decided by Claude under Stewart's delegation
- Task: BL-2470

## Context

BL-2439 sent `CURLOPT_MIMEPOST` through `Curl.Core`'s `MultipartFormBodyBuilder`, whose
`MultipartFormPart` carried text (a `string`, re-encoded in the builder's text encoding) and file paths
only. A `curl_mime_data_cb` part (`EasyMimeCallback`) threw `NotSupportedException` in
`MultipartFormPartMapping.FromMimeBody`, and `curl_mime_data` bytes (`EasyMimeBytes`) that were not
valid UTF-8 were altered by the decode. The curl tool's `-F` text reaches the same mapping as UTF-8
`EasyMimeBytes` and must still be sent in the platform's encoding (ADR-0027: the ANSI code page on
Windows), so the mapping cannot simply send every `EasyMimeBytes` verbatim.

## Decision

1. `MultipartFormPartKind` gains `Bytes` (`MultipartFormPart.Bytes`, sent as it is) and `Reader`
   (`MultipartFormPart.OpenContent`, a `Func<Stream>` called once while building, and
   `MultipartFormPart.ContentLength`, `null` for unknown). Both choose their headers as a `Text` part
   does, as libcurl's `Curl_mime_prepare_headers` treats data and callback parts alike. A `Reader`
   part is never read ahead, so an encoder streams it (`7bit` fails the read that reaches a byte above
   127), and an unknown length leaves the body's length unknown, which sends it chunked with
   `Expect: 100-continue`, as libcurl does. `Curl.Core` does not reference `Curl.Api`.
2. `MultipartFormPartMapping` maps `EasyMimeBytes` that are valid UTF-8 to text, as before, and any
   other bytes to a `Bytes` part. The engine's builder is UTF-8, so valid UTF-8 text is re-encoded to
   the same bytes and every `curl_mime_data` part goes out byte for byte; the tool's `-F` text, always
   valid UTF-8, keeps its platform encoding. Telling the mapping which caller it serves was the
   alternative; it would have changed `Curl.Console`, and the rule above gives both callers the right
   bytes without it.
3. An `EasyMimeCallback` maps to a `Reader` part over a `MimeCallbackReadStream`, a
   `CallbackReadStream` over the part's read callback. The perform collects each one opened while the
   body is built and, as for the upload's read callback (ADR-0573), a `CURL_READFUNC_ABORT` from any of
   them ends the perform with `CURLE_ABORTED_BY_CALLBACK` and `operation aborted by callback`.
4. A part with a seek callback can seek, so the multipart body around it is rewound for a resend:
   position 0 after bytes were read calls the seek callback with `(0, SEEK_SET)`, as libcurl's
   `mime_part_rewind` does, and a result other than `CURL_SEEKFUNC_OK` fails the rewind with an
   `IOException` carrying `seek callback returned error <n>`. Without a seek callback the body cannot
   seek and a resend goes on from where the read is, as for any unseekable body today.

## Consequences

- `curl_mime_data_cb` parts and binary `curl_mime_data` parts are sent byte-identical to libcurl.
- A read callback's `CURL_READFUNC_PAUSE` in a MIME part fails the read (the stream has no pause
  gate), as the synchronous upload path does.
- A failed seek of a MIME part during a resend surfaces as the HTTP handler's handling of a failed
  rewind of a seekable body, not yet as libcurl's `CURLE_SEND_FAIL_REWIND` (65).
