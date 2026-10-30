# ADR-1092: The compression dictionary extension's wire format and store (BL-4302)

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation.

## Context

BL-4302 builds the RFC 9842 (Compression Dictionary Transport) wire format for the CompressionDictionary
extension (ADR-0999) before its option and hook (BL-4132). RFC 9842 leans on RFC 8941 structured fields
and on WHATWG URLPattern, and leaves storage to the client.

## Decision

- `Use-As-Dictionary` is parsed by a hand-written RFC 8941 dictionary parser. A syntax error, a missing or
  non-string `match`, a `match-dest` that is not an inner list of strings, an `id` that is not a string or is
  longer than 1024 characters, or a `type` other than the token `raw` makes the whole header ignored.
  Parameters are checked for syntax and dropped.
- `match` is resolved against the dictionary URL with `System.Uri` and implements the URLPattern subset the
  RFC needs: same origin only, `*` matches any run of characters, `:name` one path segment (one query value),
  a pattern without a query matches any query, and a pattern with a regexp group `(` is ignored.
- Curl has no request destinations, so `match-dest` is kept but takes no part in matching; among matching
  dictionaries the longest `match` wins, then the most recently fetched (section 2.2.2).
- The store keeps `<sha256 hex>.dictionary` (the bytes) and `<sha256 hex>.metadata` (six text lines) per
  dictionary in a directory; a corrupt metadata file reads as no dictionary. Expiry by the dictionary
  response's freshness is left to BL-4132, which owns the transfer.

## Consequences

The library meets the quality gates with no package. A pattern feature outside the subset (`{}` groups,
`?`/`+` modifiers) is matched literally; a later task can widen the subset if a server needs it.
