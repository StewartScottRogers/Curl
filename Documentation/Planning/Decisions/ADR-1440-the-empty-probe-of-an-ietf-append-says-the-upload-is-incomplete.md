# ADR-1440: The empty probe of an IETF append says the upload is incomplete

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6044)

## Context

curl's HTTP handler sends a request that has a body as a probe with `Content-Length: 0` when
Digest is the one scheme allowed or an NTLM handshake starts, and sends it again with the body
when the probe draws a 2xx. The `--resumable-upload` append is such a request. For the IETF draft
its header fields say `Upload-Complete: ?1`, so a server that asks for no authentication may
complete the upload with no bytes, or reject the probe, which ends the transfer before the body
is sent. A tus server appends nothing for the empty `PATCH`.

## Decision

`HttpRequestOptions.AuthProbeHeaders` gives the header values a probe carries in place of
`Headers`. No command-line option sets it, so curl's own transfers probe as before. The resumable
upload sets it on a request with content: for the IETF draft the append's fields with
`Upload-Complete: ?0`, for tus the append's own. The request that carries the content says
`Upload-Complete: ?1`, as before.

## Why

An append with `Upload-Complete: ?0`, the right offset and no content appends nothing and
completes nothing, so a server that accepts the probe then receives the whole upload, and a server
that challenges it is answered as before. The handler's authentication flow is unchanged.

Rejected:

- Skipping the probe. It would send a large upload ahead of the authentication it needs, and an
  early 401 in an NTLM handshake can close the connection the handshake belongs to.
- Appending with `?0` and completing with an empty `?1` request of its own. It costs a request,
  the extension cannot see the proxy's allowed schemes to know when a probe is possible, and the
  server still receives an empty append that says the upload is complete.
- Widening the allowed schemes to avoid the probe. A server could then be answered with a scheme
  the user did not ask for.
