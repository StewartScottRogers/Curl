# Curl.Extension.OAuth2.UnitLibrary

The OAuth2 extension (ADR-0999, ADR-1025, ADR-1075). Namespace `Curl.Extension.OAuth2`. It references
the base class library, `Curl.Protocol.Abstractions` and `Curl.Extension.Abstractions`. It adds no URL
scheme, so it has no line in `ExtensionCatalogue.cs`; its options, description and transfer hook have
lines in the option, description and transfer hook catalogues.

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

`Http/` sends those requests (ADR-1030): `OAuth2HttpClient.PostFormAsync` opens a connection through
the injected `IConnector` (TLS for `https`), `OAuth2HttpRequestWriter` writes one HTTP/1.1 form POST
with `Accept: application/json` and `Connection: close`, and `OAuth2HttpExchange` skips interim 1xx
responses and reads the final one whole through `OAuth2HttpConnectionReader`,
`OAuth2HttpResponseHead` and `OAuth2HttpBodyReader` (`Content-Length`, chunked or the close, at most
1 MiB). Any status, a 400 or 401 included, is an `OAuth2HttpResponse` with its UTF-8 body; every
failure is an `OAuth2HttpResult` with an `OAuth2HttpFailure` and curl's exit code for it.

`Cache/` keeps tokens on disk (ADR-1034): `OAuth2TokenCache` finds a token by `OAuth2TokenCacheKey`
(token endpoint, client ID, scope asked for), gives back an unexpired one, refreshes an expired one
(RFC 6749 §6) through `OAuth2HttpClient`, and drops it on `invalid_grant` or when it has no refresh
token. Expiry is `expires_in` by the injected `TimeProvider`, 30 seconds early. `OAuth2TokenCacheFile`
reads `curl-oauth2-tokens.json` whole (missing or corrupt reads as empty, `OAuth2TokenCacheFormat`)
and writes it atomically, owner-only off Windows; `OAuth2TokenCacheLocation` finds its default
directory from curl's per-user file variables.

`Flows/` gets the token (ADR-1045). Each grant is an `IOAuth2TokenFlow` built with an
`OAuth2FlowRequest` (token endpoint, client, scope) and ends in an `OAuth2FlowResult`: the token, or
curl's exit code and message (an error response is 67, `expired_token` or a lapsed device code 28,
an unusable answer 8). `OAuth2TokenEndpoint` posts a form and reads the answer for all of them.
`OAuth2ClientCredentialsFlow` makes one request (RFC 6749 §4.4). `OAuth2DeviceAuthorizationFlow`
asks for a device code, writes where to enter the user code to the given writer (curl's stderr), and
polls every `interval` seconds on the injected `TimeProvider`, 5 more after each `slow_down`, until
a token, a refusal or `expires_in` (RFC 8628). `OAuth2AuthorizationCodeFlow` listens on
127.0.0.1 through the injected `IConnectionListener`, writes the PKCE authorization URL, accepts one
redirect (`OAuth2RedirectReceiver`, `OAuth2Redirect`), checks its `state` and exchanges the code
with the verifier (RFC 6749 §4.1, RFC 7636, RFC 8252 §7.3). `OAuth2CachedTokenSource` tries the
cache and its refresh first, runs the flow only when they give nothing, and caches the new token.

`Options/` turns it on (ADR-1075). `OAuth2Options` holds the long options: one flow option,
`--oauth2-client-credentials`, `--oauth2-device` or `--oauth2-pkce`, names the token endpoint, and
`--oauth2-client-id`, `--oauth2-client-secret`, `--oauth2-scope`, `--oauth2-device-url`,
`--oauth2-authorize-url`, `--oauth2-redirect-port` and `--oauth2-token-cache` complete it.
`OAuth2Configuration` reads them (a missing or wrong one is an `OAuth2OptionException`, which fails
the transfer with exit 2) and makes the flow. `OAuth2TransferHook` gets the token through
`TransferHookServices`' connector and listener, from the cache by default, and sends the transfer
with `Authorization: Bearer <token>` added after curl's own headers; a failed flow fails the
transfer with its exit code, and the request is never sent.

Tested against the RFCs, not against curl. Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.OAuth2.UnitTests`.
