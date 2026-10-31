# Curl.Extension.HttpCache.UnitLibrary

The HTTP cache extension's RFC 9111 core (ADR-0999, ADR-1020). Namespace `Curl.Extension.HttpCache`.
It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `HttpCacheExtension` gives the `--cache-dir <dir>` option, its description and its hook activator, which
  `Curl.Extension.Composition` registers (ADR-1069).
- `HttpCacheTransferHook` answers a fresh stored response without the origin (§4.2), revalidates a stale one
  and writes it in place of a 304 that selects it (§4.3), and stores what §3 allows; `CacheableRequest`
  decides which transfers it handles (plain GET of the whole resource) and `StoredResponseOutput` writes a
  stored response to the transfer's outputs.

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
