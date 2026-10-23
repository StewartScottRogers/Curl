# ADR-0780 — Content and transfer decoding turn off separately

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3192
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0 keeps `CURLOPT_HTTP_CONTENT_DECODING` and `CURLOPT_HTTP_TRANSFER_DECODING` apart
(`data->set.http_ce_skip` and `http_te_skip`): with content decoding 0 no Content-Encoding writer is
added, so a gzip, broken gzip or unknown coding is written as received with exit 0; with transfer
decoding 0 a chunked body is written with its chunk framing. The curl tool sets both at once with
`--raw`, whose chunked output curl 8.22.0 was measured writing through the zero-length chunk and
its trailers, exit 0 (BL-3162 Notes). Curl had one `HttpRequestOptions.Raw`, set only when both were
0, so either option alone did nothing (Stewart's report 22: exit 61 on a broken gzip, 11 decoded
bytes instead of the raw chunked stream). A single option cannot be set from the curl tool, so its
behaviour is read from the 8.22.0 source; each half's output is the half of `--raw` measured.

## Decision

`HttpRequestOptions.Raw` becomes `PassesContentCoding` and `PassesTransferCoding`.
`HttpRequestOptionsMapping` sets each from its own easy option; `--raw` sets both, as before.
`HttpProtocolHandler` decodes Content-Encoding unless `PassesContentCoding`, and frames, keeps alive
and writes the body by `PassesTransferCoding` where it used `Raw`.

## Consequences

`--raw` is unchanged. Through the easy API each option now works alone: content decoding 0 with
`CURLOPT_ACCEPT_ENCODING` set still sends `Accept-Encoding` but writes the coded bytes; transfer
decoding 0 writes the chunk framing through the zero-length chunk and its trailers.
