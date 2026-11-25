# ADR-1293: A SOCKS handshake reset is exit 97 "Recv failure"

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5382).

## Context
A SOCKS proxy that reset the connection during the handshake escaped as an `IOException` and
ended as exit 2. curl gives exit 97 `Recv failure: Connection was reset` (Schannel build).

## Decision
`SocksProxyTunnel.OpenAsync` catches an `IOException` that holds a socket error and returns exit 97 with
`CurlSocketErrorText.ReceiveFailure`'s text, proxy code `ReceiveConnect`. A clean close keeps
`Failed to receive SOCKS response, proxy closed connection`. An `IOException` without a socket error escapes.
One catch covers SOCKS4, 4a, 5 and 5h at every read; the proxy code is one value for all steps.
