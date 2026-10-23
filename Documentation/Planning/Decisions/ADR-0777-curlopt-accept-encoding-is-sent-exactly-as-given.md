# ADR-0777 — CURLOPT_ACCEPT_ENCODING is sent exactly as given

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3191
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0's `lib/http.c` sends `Accept-Encoding: <value>` with the `CURLOPT_ACCEPT_ENCODING`
string as set; only an empty string is first replaced (`lib/setopt.c`) by every content encoding the
build decodes, and NULL sends no line. Any value but NULL turns content decoding on. Curl sent
`deflate, gzip, br, zstd` for every non-NULL value. The curl tool can only set the empty string
(`--compressed`), so the non-empty values cannot be measured through `Record-CurlExchange.ps1`; the
behaviour is read from the 8.22.0 source, and the empty string's list is the one ADR-0287 measured.

## Decision

`HttpRequestOptions` gains `AcceptEncoding`, the value sent while `Compressed` is set, defaulting to
`HttpRequestOptions.DefaultAcceptEncoding` (`deflate, gzip, br, zstd`). `HttpRequestOptionsMapping`
puts a non-empty `EasyHttpOptions.AcceptEncoding` there unchanged and the default for an empty one;
`Compressed` stays "set at all", so decoding stays on for any value, `identity` included, as libcurl's.

## Consequences

The curl tool's `--compressed` sends what it did. A value naming an encoding Curl cannot decode is
still sent as given; the response is decoded as its `Content-Encoding` says, as before.
`CURLINFO_REQUEST_SIZE` follows the request sent.
