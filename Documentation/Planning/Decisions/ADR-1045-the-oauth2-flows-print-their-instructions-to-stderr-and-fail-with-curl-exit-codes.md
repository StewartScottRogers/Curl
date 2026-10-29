# ADR-1045: The OAuth 2.0 flows print their instructions to standard error and fail with curl's exit codes

- Status: Accepted
- Date: 2026-10-29
- Decision: Decided by Claude under Stewart's delegation (BL-4210)

## Context

The OAuth2 extension (ADR-1025) gets an access token by the client credentials grant (RFC 6749 §4.4),
the device authorization grant (RFC 8628) and the authorization code grant with PKCE (RFC 6749 §4.1,
RFC 7636) through a loopback redirect (RFC 8252 §7.3), trying the token cache and a refresh first
(ADR-1034). Upstream curl has no such feature, so there is no curl output to match; what the flows tell
the user and how they fail must still fit curl: diagnostics on standard error, a failure as one of
curl's exit codes with a message the host prints as `curl: (N) message`.

## Decision

Every flow (`Curl.Extension.OAuth2.Flows`, `IOAuth2TokenFlow`) ends in an `OAuth2FlowResult`: the token,
or curl's exit code and a message. `OAuth2CachedTokenSource` asks the cache first (which refreshes an
expired token), runs the flow only when the cache has nothing usable, and caches the token it gets.

What the flows print, each line ending in a newline, to the writer the host passes (curl's standard
error, so the transfer's standard output stays the response body):

- Device authorization (RFC 8628 §3.3): `To sign in, open <verification_uri> and enter the code <user_code>`,
  and, when the server sent `verification_uri_complete` (§3.3.1), a second line
  `or open <verification_uri_complete>, which carries the code.`
- Authorization code: `To sign in, open this address in a browser:` and the authorization URL on the
  next line. The browser's redirect is answered with a `text/plain` page, `curl has the sign-in
  response. You can close this window.` (200), or `curl could not read this request.` (400).

Exit codes:

| Failure | Exit code |
|---|---|
| Connecting, sending or reading an endpoint's answer | the HTTP client's (ADR-1030), such as 7 or 56 |
| An endpoint answers with an OAuth 2.0 error response (RFC 6749 §5.2, RFC 8628 §3.5): `invalid_client`, `invalid_grant`, `access_denied` and the rest | 67 (`CURLE_LOGIN_DENIED`) |
| `expired_token`, or `expires_in` passed while polling | 28 (`CURLE_OPERATION_TIMEDOUT`) |
| An answer that is neither the expected response nor an error response | 8 (`CURLE_WEIRD_SERVER_REPLY`) |
| The redirect's `state` is missing or not the one sent (RFC 6749 §10.12) | 67 |
| The redirect carries `error` (§4.1.2.1) | 67 |
| The redirect carries neither `code` nor `error`, or is not an origin-form `GET` | 8 |
| The loopback port cannot be bound, or the accept fails | the listener's |

The refusal message is `The <endpoint> refused the request: <error> (<error_description>).`, naming the
token endpoint, device authorization endpoint or authorization server.

Device polling waits `interval` seconds (at least 1) on the injected `TimeProvider` before each token
request, adds 5 seconds on each `slow_down` (§3.5), keeps polling on `authorization_pending`, and stops
on any other answer or once `expires_in` has passed since the device authorization response.

The authorization code flow listens on 127.0.0.1 at the port the caller gives (0 for any free one),
sends `redirect_uri=http://127.0.0.1:<port>/`, a 32-octet `code_verifier` and a 16-octet (128-bit)
`state` from the random source the caller gives, accepts exactly one connection, and reads at most
16 KiB of its request head.

## Consequences

- Exit code 67 already means "the server refused the login" to scripts; a refused grant is that.
- 28 for an expired device code matches what a user waiting too long sees from curl's own timeouts.
- A browser's later request (a favicon) after the one redirect finds the port closed; the page has
  already been answered.
