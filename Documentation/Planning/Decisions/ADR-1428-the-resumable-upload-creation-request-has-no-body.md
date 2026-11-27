# ADR-1428: The resumable upload creation request has no body

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6034, fitness finding EXT-129)

## Context

`--resumable-upload` sends each of its requests through curl's transfer (ADR-1379). The
creation request carried an empty upload stream. curl's HTTP handler sends a request that
has a body, however empty, as a probe when Digest is the one scheme allowed or an NTLM
handshake starts, and sends it again when the probe draws a 2xx, as curl does. A server
that asks for no authentication therefore got two creation requests and kept an empty,
orphaned upload resource.

## Decision

The creation request has no body. It carries `Content-Length: 0` as a header field, after
the protocol's own fields and before the `-H` ones. A request without a body also drops a
`-H "Transfer-Encoding: ..."` line, which would give it a chunked body again.

## Why

A request without a body is never probed: it goes once, and a 401 is answered by the
authenticated request as for any other request, the rejected one creating nothing. The
handler stays as curl behaves. `Content-Length: 0` stays on the wire for servers that
require a length on `POST` or `PUT`.

The append `PATCH` still has a body, so curl's probe applies to it; that is a separate
matter from the creation (BL-6044).
