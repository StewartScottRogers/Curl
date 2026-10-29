# Curl.Extension.OAuth2.UnitLibrary

The OAuth2 extension (ADR-0999, ADR-1022). Namespace `Curl.Extension.OAuth2`. It references the
base class library and `Curl.Protocol.Abstractions`. Not registered in the extension catalogue yet:
the options and the flows arrive in BL-4161.

`Wire/` writes and reads every message of the client credentials, authorization code with PKCE and
device authorization flows:

- `OAuth2Pkce` makes the `code_verifier` from random octets the caller supplies (RFC 7636 §4.1) and
  its `S256` `code_challenge` (§4.2), reproducing Appendix B.
- `OAuth2FormEncoder` writes `application/x-www-form-urlencoded` text as RFC 6749 Appendix B says.
- `OAuth2TokenRequestWriter` writes the client credentials (§4.4.2), authorization code with
  `code_verifier` (§4.1.3, RFC 7636 §4.5), refresh (§6) and device code (RFC 8628 §3.4) token
  requests and the device authorization request (RFC 8628 §3.1) as an `OAuth2FormRequest`: the body,
  and the `Authorization` header when an `OAuth2Client` with a password uses HTTP Basic (§2.3.1).
  A client with no password sends its `client_id` in the body.
- `OAuth2AuthorizationRequest.BuildUrl` builds the authorization URL with `state` and the challenge
  (§4.1.1, RFC 7636 §4.3), keeping any query the endpoint has.
- `OAuth2TokenResponseParser`, `OAuth2ErrorResponseParser` and `OAuth2DeviceAuthorizationResponseParser`
  read the token (§5.1), error (§5.2, RFC 8628 §3.5) and device authorization (RFC 8628 §3.2,
  `interval` defaulting to 5) bodies with `JsonDocument`. Each returns an `OAuth2ParseResult<T>`: a
  body that is not a JSON object, lacks a required member or has a member of the wrong type is a
  failure with its reason, never an exception. `OAuth2JsonBody` holds the shared reading.

Tested against the RFCs, not against curl. Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.OAuth2.UnitTests`.
