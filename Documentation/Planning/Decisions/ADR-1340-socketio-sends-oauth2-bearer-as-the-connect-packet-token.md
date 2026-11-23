# ADR-1340: The socketio client sends --oauth2-bearer as the CONNECT packet's token

- Status: Accepted
- Date: 2026-11-22
- Task: BL-5781
- Decided by Claude under Stewart's delegation.
- Amends ADR-1311.

## Context

ADR-1311 maps `-u` to the namespace CONNECT packet's `auth` payload. `--oauth2-bearer` only reached the Engine.IO handshake as `Authorization: Bearer`, so a server that authenticates from `socket.handshake.auth.token` (the Socket.IO documentation's convention) refused the client (Surly bug report 2026-11-22T1734, finding 1).

## Decision

With `--oauth2-bearer abc` the CONNECT packet is `40[/ns,]{"token":"abc"}`, written by `Utf8JsonWriter`. The handshake keeps `Authorization: Bearer abc`. As in `HandshakeHeaderFields` (and curl's HTTP code), the bearer token wins over `-u` when both are given, so then only `{"token":..}` is sent. `-u` alone is unchanged. A refused token ends with exit 22 as ADR-1311 says. The token is never written to `-v` output, `--log-file` or an error message. `Curl.Api`'s bearer authentication reaches the handler through the same `HttpRequestOptions.BearerToken`.

The field name `token` is the documented convention; not measured against Surl, which the lane cannot reach.

## Consequences

`--extended-help` for `socketio` describes the mapping.
