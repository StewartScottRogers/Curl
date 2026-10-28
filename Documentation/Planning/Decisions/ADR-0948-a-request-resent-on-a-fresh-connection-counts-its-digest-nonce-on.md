# ADR-0948: A request resent on a fresh connection counts its Digest nonce on

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3874
- Decided by Claude under Stewart's delegation.

## Context

Measured on 2026-10-28 with `Record-CurlExchange.ps1` and curl 8.22.0 (Git for Windows' ucrt64,
Schannel): a server sends a 401 (`Digest realm="testrealm", nonce="abc123", qop="auth"`, an
HTTP/1.1 head with no `Connection: close`) and then closes the connection. Curl sends its answer
(`nc=00000001`) on the reused connection, sees it die before any response byte
(`Connection died, retrying a fresh connect (retry count: 1)`), and sends the request again on a
new connection with the same `cnonce`, `nc=00000002` and the response hashed for that count.
Curl sent the same `nc=00000001` value again. The task first assumed the 401 itself announced
the close; the measurement shows the count comes from curl making the Digest header again for
the resend, as `Curl_output_digest` counts on each time it is called.

## Decision

When the HTTP handler resends a request on a fresh connection because the reused one died
before the response (and for HTTP/3's refused-stream resend, which takes the same route), it
gives the resend the `Authorization` value `IHttpAuthenticator.RepeatAuthorization` makes from
the one sent: a Digest answer with a `qop` counts its nonce on, and every other value is sent
as it was. `DigestAuthenticator` keeps no state (ADR-0014), so the count comes from the value
sent. This applies on every platform, not only where the SSPI build is matched: curl's own
Digest code (`lib/vauth/digest.c`) also counts on after each answer it makes. The
`Proxy-Authorization` value is left as it was, since no proxy case was measured.

## Consequences

The measured exchange gives the same `nc` and a response that verifies for it in curl and
Curl. A kept connection is unchanged: one answer, `nc=00000001`.
