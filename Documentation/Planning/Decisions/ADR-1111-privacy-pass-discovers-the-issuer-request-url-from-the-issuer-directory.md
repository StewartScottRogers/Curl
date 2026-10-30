# ADR-1111: --privacy-pass discovers the issuer request URL from the issuer directory

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4316
- Decided by Claude under Stewart's delegation

## Context

ADR-1110 gave `--privacy-pass` the issuer's request URL and left discovering it from the issuer directory (RFC 9578
§4) as a task. Upstream curl has no Privacy Pass support, so the behaviour is tested against the RFC.

## Decision

- `--privacy-pass` takes either the issuer request URL, as before, or the issuer origin. A URL whose path is `/`
  (or empty) and that has no query is an origin; any other URL is a request URL, so existing command lines keep
  working. An issuer request URL at the root of its origin cannot be given directly; its origin discovers it.
- For an origin, when a `PrivateToken` challenge is answered, the extension GETs
  `/.well-known/private-token-issuer-directory` at that origin with
  `Accept: application/private-token-issuer-directory`, reads the JSON object with the BCL's `JsonDocument`, and
  resolves `issuer-request-uri` against the directory's URL (it may be relative).
- The directory's `token-keys` entries of token type 2 are the issuer's keys: the challenge's `token-key` must be one
  of them, otherwise the transfer fails with exit 8 and no token is requested. This keeps a challenge from naming a
  key the issuer does not publish.
- `not-before` is not read: a key not yet valid only makes the issuer refuse the request, which already fails.
- The directory is fetched once per answered challenge and not cached; a directory request answered non-2xx is exit
  67, as every issuer answer without a token (ADR-1110); a body that is not a directory object is exit 8; transport
  failures keep their exit code.
