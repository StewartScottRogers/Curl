# ADR-1020 — The HTTP cache is a private cache of one file per request key

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4203.

## Context

`--cache-dir` (BL-4133) is an extension (ADR-0999) that keeps HTTP responses in a directory
under RFC 9111. Upstream curl has no cache, so there is no curl behaviour to match; the
specification decides. BL-4203 builds the rules and the on-disk entry as pure code in
`Curl.Extension.HttpCache.UnitLibrary`, before any option or hook exists.

## Decision

- **A private cache by default.** One user's cache directory is a private cache (RFC 9111 §1):
  `private` and a request's `Authorization` do not stop storing. The shared-cache rules
  (§3.5, `s-maxage`, `proxy-revalidate`, `private`) are kept behind an `isSharedCache` flag on
  `ResponseStorability` and `FreshnessCalculator` so a later option can turn them on.
- **What is stored.** Only GET responses with a final status from 200 to 599 other than 206
  and 304; this cache neither combines partial content nor stores a 304 as a new response.
- **Stale responses.** `FreshnessCalculator.Evaluate` reports `Fresh`, `Stale` (usable if
  validation cannot reach the server, §4.2.4) or `MustValidate` (`no-cache`, qualified or not,
  which §5.2.2.4 allows a cache to treat alike; or stale with `must-revalidate`).
- **Heuristic freshness** is a tenth of the time from `Last-Modified` to `Date` (§4.2.2's
  suggestion), for heuristically cacheable statuses and `public` responses.
- **Validation** sends both `If-None-Match` and `If-Modified-Since` when the stored response has
  both validators. A 304 selects the stored response when its `ETag` matches by weak comparison,
  else its `Last-Modified` is the same, else neither has a validator. The 304's fields replace
  the stored fields of the same name, except `Content-Length` and hop-by-hop fields (§3.2).
- **The key** is SHA-256 over the method, a space and the target URI without user information
  or fragment (§2), as 64 lowercase hex digits: a file name that is safe on every platform and
  has no length or character limits to escape.
- **The entry file** is `<key>.entry`: a magic line `CURL-HTTP-CACHE 1`, then `BinaryWriter`
  fields (status code, status line, request and response times as UTC ticks, headers, body).
  A write goes to a temporary file and is moved over the entry, so readers never see half an
  entry; any file that does not decode exactly, with nothing after the body, reads as no entry.
  `Vary` (§4.1) is left to the client flow that BL-4133 adds.

## Consequences

The library references only the base class library, so `VerifyExtensionIsolation` holds, and
it is unit tested without a network; its file tests use a temporary directory they delete.
