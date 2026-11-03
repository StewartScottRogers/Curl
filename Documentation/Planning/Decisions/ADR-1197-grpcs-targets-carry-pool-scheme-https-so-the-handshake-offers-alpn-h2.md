# ADR-1197: A grpcs:// target carries PoolScheme https so its handshake offers ALPN h2

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4562)

## Context

gRPC over TLS runs as HTTP/2 `h2` (PROTOCOL-HTTP2.md, requirement `Grpc:Http2-Tls-1`). The gRPC handler's
target asked for `ApplicationProtocols = ["h2"]`, but `TcpConnector.ApplicationProtocolsFor` offers a target's
application protocols only when its `PoolScheme` is `https`, so a `grpcs://` ClientHello carried no ALPN
extension and every call failed with exit 16. The fix could go in the extension or in `Curl.Networking`.

## Decision

`GrpcProtocolHandler` gives a `grpcs://` target `PoolScheme = "https"`; a `grpc://` target keeps none.

- It stays inside the extension: an extension never changes curl's behaviour (ADR-0999), and widening
  `ApplicationProtocolsFor` would change what every other TLS scheme offers.
- An `https` scheme is what the call is on the wire (`:scheme: https`, HTTP/2 over TLS), so the
  connector's https-origin behaviour (ALPN offer, `-v` and trace lines) is the right one.
- The handler never marks its connection reusable, so it is never put back in the pool. A `grpcs://`
  call after an `https://` transfer to the same host and port in one run may be handed that idle
  connection; one that negotiated `h2` takes the call, one that did not ends with the handler's exit 16,
  as a fresh connection to a server without `h2` would.

## Consequences

`GrpcTlsCases.Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` passes and `Grpc:Http2-Tls-1` leaves
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.

## Amendment (BL-4680): a grpcs:// target is never pooled

The Decision's last bullet above, which let a `grpcs://` call be handed an idle `https://` connection to the same host and port, is replaced by this. `GrpcProtocolHandler` also sets `ConnectTarget.IsNeverPooled` on a `grpcs://` target, which makes `ConnectionPoolKey.Of` return `null`: the pool neither serves nor shares a pooled `https://` connection to a gRPC call, and never keeps the gRPC connection. Without it the two shared one pool key, because the key ignores `ApplicationProtocols`: an HTTP/1.1 connection failed the call with exit 16, and an h2 one got a second connection preface in a live session. A distinct pool scheme was rejected because the `https` checks in `TcpConnector` and the extension connectors would each need to learn it.
