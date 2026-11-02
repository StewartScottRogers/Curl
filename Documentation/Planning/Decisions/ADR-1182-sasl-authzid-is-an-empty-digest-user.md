# ADR-1182: --sasl-authzid without -u is an empty Digest user

- Status: Accepted
- Date: 2026-11-01
- Amends ADR-0441
- Decided by Claude under Stewart's delegation (BL-4457, AF-0522)

## Context

ADR-0441 holds a Digest POST's or PUT's body back for an empty probe when the transfer has credentials. The
audit office found (AF-0522) that curl 8.22.0 also probes for `--digest --sasl-authzid 1 -d x` with no `-u`:
`-v` logs `* Server auth using Digest with user ''`, the first request is `POST` with `Content-Length: 0`, and
the body follows with the answer. Curl had no credential there, so it sent the body at once. Measured on curl
8.22.0 with `Record-CurlExchange.ps1` (BL-4457 Notes): `--digest --sasl-authzid 1` (GET) logs the same line;
`--sasl-authzid 1` alone and `--basic --sasl-authzid 1` send no `Authorization` and no auth line. So curl
counts the authzid as an empty user for Digest only.

## Decision

- `HttpRequestOptions.SaslAuthorizationIdentity` carries `--sasl-authzid` to the HTTP handler;
  `HttpRequestOptionsMapping.Of` copies it from `EasyAuthenticationOptions.SaslAuthorizationIdentity`.
- With no transfer credential, a set authzid and Digest the one scheme allowed, the origin's `HttpAuthRequest`
  gets an empty user name and password (`HttpProtocolHandler.OriginCredentialOf`), so ADR-0441's probe goes
  and the challenge is answered for user `''`.
- Any other scheme set (Basic, `--anyauth`) is unchanged: no credential, so no `Basic Og==`. A proxy is
  unchanged: `--sasl-authzid` is not a proxy option.

## Consequences

The finding's reproduction sends the same `Content-Length: 0` probe as curl. `--anyauth --sasl-authzid` was
not measured and keeps its old behaviour until a measurement says otherwise.
