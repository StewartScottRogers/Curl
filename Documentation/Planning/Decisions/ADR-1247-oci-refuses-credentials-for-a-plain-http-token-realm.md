# ADR-1247 — oci:// refuses to send -u credentials to a plain http token realm

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4901
- Decided by Claude under Stewart's delegation.

## Context

The oci:// scheme always reaches the registry over HTTPS. A registry's 401 names a token realm
in its `WWW-Authenticate: Bearer` challenge, and `OciTokenRequestBuilder` accepted any absolute
`http` or `https` realm. With `-u`, the Basic credentials went to an `http://` realm in clear text
(fitness review finding EXT-006). This is the same downgrade curl refuses when a redirect would
carry credentials from HTTPS to HTTP (CVE-2022-27774). Upstream curl has no OCI scheme, so there
is no curl output to measure.

## Decision

- When `-u` gave credentials and the realm is plain `http`, `OciTokenRequestBuilder.Build`
  throws `OciTransferException` before it builds any request. The transfer fails with exit 67
  (`CURLE_LOGIN_DENIED`, the code the session already uses when a token endpoint gives no token)
  and the message `Refusing to send credentials to the non-HTTPS token realm <realm>.`. That
  text is Curl's own.
- An anonymous token request (no `-u`) to an `http` realm still goes out, because it carries no
  credentials.
- An `https` realm gets the credentials as before.

## Consequences

A registry that points a credentialed client at a plain-HTTP token service now fails instead of
leaking the password. Anonymous pulls from such registries keep working.
