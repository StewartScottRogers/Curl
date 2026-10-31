# ADR-1066: A connection hook's TLS offers curl's ALPN list

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4263)

## Context

ADR-1055 runs curl's TLS handshake over an extension's own stream (`--npipe`) through the public
`ITlsProvider.AuthenticateAsClientAsync(plaintext, host, events, token)`, which offers no ALPN, so an
`https://` transfer over a pipe could only speak HTTP/1.1 and never told the server so. The overload that
offers ALPN lives on `Curl.Networking`'s internal `IHandshakeReportingTlsProvider`, and the list curl offers
when a target names none (`--http1.1`, `--http2`, the platform default, ADR-0141) is `TcpConnector`'s
`HttpOverTlsApplicationProtocols`, which the HTTP handler never puts on `ConnectTarget`: only an Alt-Svc
switch or a downgrade sets `ConnectTarget.ApplicationProtocols`. An extension sees neither.

## Decision

- `ITlsProvider` gains `AuthenticateAsClientAsync(plaintext, host, events, applicationProtocols, token)`, a
  default member giving the four-argument handshake (offering nothing), so every existing provider and fake is
  unchanged. `SslStreamTlsProvider` and `HandBuiltTlsProvider` implement it as their six-argument origin
  handshake (`isProxy: false`), which still drops the offer under `--no-alpn`; `CertificateCheckingTlsProvider`
  passes it to curl's provider and checks the result as it checks the others.
- `IConnector` gains `HttpOverTlsApplicationProtocols`, a default member giving `http/1.1` alone (curl's offer
  with no version option on Windows). `TcpConnector`'s existing public property implements it, and
  `PoolingConnector` gives its inner connector's, so the connector curl hands a connection hook says what curl
  would offer.
- `NamedPipeConnectionHook.ConnectorOver(curlConnector, curlTlsProvider)` hands that list to
  `NamedPipeConnector`, which offers, as `TcpConnector.ApplicationProtocolsFor` does for an origin, the target's
  own `ApplicationProtocols` or else the list for an `https` pool scheme, and nothing for any other TLS scheme.

The list travels on the connector rather than on a new `IConnectionHook` overload or on every `ConnectTarget`
because the connector already owns it: no host or engine change is needed, and any later hook that layers TLS
reads it the same way.

## Consequences

- `curl --npipe \.\pipe\name https://host/` offers `http/1.1` on Windows, `h2,http/1.1` under `--http2`,
  and nothing under `--no-alpn`, as `--unix-socket` does, so HTTP/2 can be negotiated over the pipe.
- A forward proxy and the system-store-only trust of CGR-289 do not apply over a pipe, which has neither.
