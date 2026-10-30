# ADR-1154: --compression-dictionary stores and advertises RFC 9842 dictionaries in a transfer hook (BL-4132)

- Status: Accepted
- Date: 2026-10-30
- Decided by Claude under Stewart's delegation.

## Context

BL-4132 finishes the CompressionDictionary extension (ADR-0999): ADR-1100 built the wire format and store,
ADR-1107 the `dcz` decoder and ADR-1114 the `dcb` decoder. What was left is the option and the client flow of
RFC 9842 (Compression Dictionary Transport, September 2025), the version followed.

## Decision

- One option, `--compression-dictionary <dir>`, and one extension name, `compression-dictionary`. The option names
  the store directory; there is no scheme. It is a transfer hook (ADR-1007, ADR-1019), registered by one line in
  each of the option, description and transfer hook catalogues, as `--cache-dir` is; `ExtensionCatalogue.cs` holds
  protocol handlers only, so it gets no line.
- The hook takes part in HTTP and HTTPS transfers of a whole body (not `-I`, a range or a resume). For those it
  buffers the body curl's handler received, so it can decode and store it.
- A successful 2xx response whose `Use-As-Dictionary` parses (section 2.1) is stored with its decoded body, keyed
  by the request URL.
- On a request the best stored dictionary matches (section 2.2.2) the hook sends `Available-Dictionary`,
  `Dictionary-ID` when the dictionary has an id, and `Accept-Encoding: dcb, dcz` (sections 2.2, 2.3, 6).
- A response with `Content-Encoding` exactly `dcb` or `dcz` to such a request is checked against the advertised
  hash and decoded (sections 4, 5). A header naming another dictionary or corrupt data fails the transfer with
  exit 61, `CURLE_BAD_CONTENT_ENCODING`, the code curl gives a body it cannot decode. Any other coding is written
  as received.
- With `--compressed` and a matching dictionary (BL-4351), the hook sends `Accept-Encoding` as curl's
  `--compressed` value (`deflate, gzip, br, zstd`, or the `CURLOPT_ACCEPT_ENCODING` string given) followed by
  `dcb, dcz`, and runs curl's handler through `TransferContextContentDecoding.WithContentDecodingOff`, a
  `Curl.Extension.Abstractions` extension that hands it `HttpRequestOptions` with `Compressed` off, because curl's
  handler refuses `dcb` and `dcz` with exit 61. The hook then decodes every coding the response's `Content-Encoding`
  fields name, last applied first, through `CompressedResponseBody`: `dcb` and `dcz` against the dictionary, and
  `gzip` (`x-gzip`), `deflate` (zlib, or raw deflate when the first bytes are no zlib header), `br` and `zstd` as
  curl does; `identity` and `none` are skipped, and any other coding or corrupt data is exit 61. Without a
  matching dictionary, curl's handler decodes as it always does.
- The hook advertises nothing when `-H` sets `Accept-Encoding` (the user has chosen the codings) or when
  `--compressed` is given with content decoding off (`--raw`, which asks for the body as the server encoded it).
- A store directory that cannot be written leaves the transfer as it was.

## Left out, and why

- Dictionary expiry by the dictionary response's freshness (section 2.1, RFC 9111): curl keeps no cache
  freshness outside `--cache-dir`, so a stored dictionary stays until it is deleted or replaced by the same hash.
- `match-dest` (section 2.1.2): curl has no request destinations (ADR-1100).
- Without `--compressed`, a `Content-Encoding` that lists `dcb` or `dcz` with another coding: servers send them
  alone (section 6), and curl without `--compressed` decodes nothing.
- When the hook decodes curl's codings itself under `--compressed`, the exit 61 message is the hook's
  (`Failed to decode the <codings> body: ...`) rather than curl's per-coding text, a `zstd` frame's window is held
  to 8 MiB, and a `--max-filesize` limit is checked by curl's handler against the encoded size: the extension is
  tested against RFC 9842, not curl, and these differ only on bodies that fail or exceed a limit.

## Consequences

Without `--compression-dictionary`, and with `CURL_EXTENSIONS=0` (which refuses the option with exit 2), no
transfer changes. With it, the body of an HTTP transfer is written once the transfer has ended, not as it
arrives.
