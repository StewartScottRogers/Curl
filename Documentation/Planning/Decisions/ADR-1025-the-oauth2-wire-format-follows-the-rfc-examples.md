# ADR-1025: The OAuth2 extension's wire format follows the RFC examples

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4212)

## Context

The OAuth2 extension (BL-4161, ADR-0999) fetches a token by the client credentials, authorization code with PKCE
and device authorization flows. Upstream curl has no such option, so there is no curl output to measure; the
messages are tested against RFC 6749, RFC 7636 and RFC 8628. Their text leaves a few choices open.

## Decision

- Form bodies and the authorization URL's query are encoded as RFC 6749 Appendix B says, as HTML's
  `application/x-www-form-urlencoded`: UTF-8, every octet but `A-Z a-z 0-9 - . _ *` percent-encoded in upper-case
  hex, a space as `+`. §4.1.3's example writes `.` as `%2E`; both decode alike, and Appendix B is the rule.
- A client with a password authenticates by HTTP Basic, the scheme §2.3.1 says every server must support, unless
  told to send `client_id` and `client_secret` in the body. The identifier and password are form-encoded before
  Base64, as §2.3.1 says. A client with no password sends `client_id` in the body (§3.2.1, RFC 8628 §3.1, §3.4).
- Parameters go in a fixed order: `grant_type`, the grant's own fields, then the client's. RFC 8628 §3.1's example
  puts `client_id` before `scope`; order carries no meaning, so the device authorization request uses the same
  order as the rest.
- The verifier is made from 32 to 96 random octets the caller supplies, base64url without padding (RFC 7636 §4.1),
  so it is 43 to 128 characters; another count is the caller's error (`ArgumentOutOfRangeException`). Only `S256`
  is written: `plain` is for clients that cannot hash (§4.2), which this one can.
- JSON responses are read with `System.Text.Json`'s `JsonDocument` (AOT-safe, no serializer). A member sent as
  JSON `null` reads as left out. `expires_in` and `interval` must be JSON integers, as RFC 6749 §5.1 and RFC 8628
  §3.2 define them; a string or fraction is a failure. Unknown members are ignored (§5.1). A malformed body is an
  `OAuth2ParseResult` failure with its reason, never an exception.
- An `error` code neither RFC names reads as `Unrecognized`, keeping the code as sent.

## Consequences

The flows in BL-4161 build on `Curl.Extension.OAuth2.Wire` and never format or parse a message themselves.
