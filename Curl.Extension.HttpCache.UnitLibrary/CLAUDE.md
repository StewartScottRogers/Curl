# Curl.Extension.HttpCache.UnitLibrary

The HTTP cache extension's RFC 9111 core (ADR-0999, ADR-1018). Namespace `Curl.Extension.HttpCache`.
It references nothing but the base class library so far; the `--cache-dir` option, its transfer
hook and its registration in `Curl.Extension.Composition` arrive with BL-4133.

- `ResponseStorability` decides whether a response may be stored (§3, §3.5).
- `FreshnessCalculator` computes the freshness lifetime (§4.2.1, §4.2.2), the current age (§4.2.3)
  and `CacheEntryUsability` against an injected `TimeProvider`.
- `CacheValidation` builds the conditional request header fields (§4.3.1), decides whether a 304
  selects the stored response and applies its header fields (§3.2, §4.3.4).
- `CacheKey` derives an entry's file name from the method and target URI (§2);
  `CacheDirectory` writes and reads one `StoredResponse` per `<key>.entry` file, encoded by
  `StoredResponseEncoding`; a corrupt or truncated file reads as no entry.
- `CacheControlDirectives`, `DeltaSeconds`, `HttpDate` and `HttpHeaderValues` parse the fields
  those rules read.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.HttpCache.UnitTests`.
