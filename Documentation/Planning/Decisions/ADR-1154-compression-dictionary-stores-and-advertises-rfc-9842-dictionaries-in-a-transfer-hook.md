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
- The hook advertises nothing when `--compressed` is given or `-H` sets `Accept-Encoding`. With `--compressed`,
  curl's handler decodes the content itself and fails on `dcb` and `dcz` with exit 61, and a hook cannot turn that
  decoding off through `Curl.Extension.Abstractions`. With `-H`, the user has chosen the codings.
- A store directory that cannot be written leaves the transfer as it was.

## Left out, and why

- Dictionary expiry by the dictionary response's freshness (section 2.1, RFC 9111): curl keeps no cache
  freshness outside `--cache-dir`, so a stored dictionary stays until it is deleted or replaced by the same hash.
- `match-dest` (section 2.1.2): curl has no request destinations (ADR-1100).
- `dcb` and `dcz` together with `--compressed`: this needs a way for a hook to take over content decoding from
  curl's handler. Without that, `--compressed` keeps working exactly as curl's.
- A `Content-Encoding` that lists `dcb` or `dcz` with another coding: servers send them alone (section 6).

## Consequences

Without `--compression-dictionary`, and with `CURL_EXTENSIONS=0` (which refuses the option with exit 2), no
transfer changes. With it, the body of an HTTP transfer is written once the transfer has ended, not as it
arrives.
