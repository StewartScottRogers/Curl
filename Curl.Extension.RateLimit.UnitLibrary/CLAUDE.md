# Curl.Extension.RateLimit.UnitLibrary

The RateLimit header fields extension (ADR-0999, ADR-1106), tested against draft-ietf-httpapi-ratelimit-headers-11.
Namespace `Curl.Extension.RateLimit`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared
layers it brings.

- `RateLimitExtension` gives the `--ratelimit-pace` flag, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `RateLimitTransferHook`, one per option group, reads each HTTP response's `RateLimit` field: a member with no
  quota left (`r=0`, §4.1.1) holds the origin for `t` seconds (§4.1.2), or for the `w` window of the
  `RateLimit-Policy` member of the same name (§3.1.3); the longest hold wins, capped at about 49.7 days. Every later
  transfer to that origin in the group, a `--retry` attempt or a `-Z` parallel transfer, waits on the transfer's
  `TimeProvider` until the hold has passed (§7). A transfer that is not HTTP runs unchanged.
- `RateLimitField` reads both fields' Structured Fields List into `RateLimitItem`s: the policy name (a String) and
  its non-negative Integer parameters.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.RateLimit.UnitTests`.
