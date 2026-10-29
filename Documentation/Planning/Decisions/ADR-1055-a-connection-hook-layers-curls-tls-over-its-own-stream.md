# ADR-1055: A connection hook layers curl's TLS over its own stream

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4206)

## Context

ADR-1021 left TLS over `--npipe` out: the connection hook (ADR-1010) replaces curl's whole connector, curl's
TLS handshake lives inside that connector, and an extension references neither `Curl.Networking` nor a
protocol library, so `https://` under `--npipe` was refused with exit 4. `--unix-socket` runs TLS over its
socket with the group's TLS options. The seam has to stay in `Curl.Extension.Abstractions` and the host,
without changing the engine's connector factory.

## Decision

- `IConnectionHook` gains `ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider)`, a default
  member that gives `ConnectorOver(curlConnector)`, so an existing hook is unchanged. `ConnectionHooks` gains
  the matching overload, and `Curl.Console`'s `CurlComposition.ExtensionConnectorOf` now hands the engine
  `curl => hooks.ConnectorOver(curl, tlsProvider)` with the option group's own TLS provider - the one curl's
  connector layers over a connection it opened, built from `-k`, `--cacert`, `--capath`, `--cert`, the TLS
  version and cipher options. The engine needs no change.
- `NamedPipeConnector` opens the pipe and, for a `ConnectTarget.UseTls` target, runs
  `ITlsProvider.AuthenticateAsClientAsync(pipe, TlsServerName ?? Host, target.Events, token)` over it, so the
  handshake reports its `-v` lines and a refused certificate is exit 60 as over a socket. The provider's
  failure is returned as it is, after the provider has closed the pipe. Given no TLS provider (a host calling
  the one-argument `ConnectorOver`), a TLS target is still refused with exit 4 before the pipe is opened.
- An extension's certificate checks (ADR-1016) still apply: the engine puts the hook's connector through
  `extensionCertificateConnector`, so the pipe is handed the unwrapped provider and the chain is checked once.

This replaces ADR-1021's first "Left out" item.

## Left out, and why

- ALPN over the pipe: the public `ITlsProvider` overloads offer no application protocols (the ALPN overload is
  `Curl.Networking`'s internal `IHandshakeReportingTlsProvider`), so a handshake over the pipe negotiates none
  and HTTP runs as HTTP/1.1. A follow-up task adds a public ALPN overload to `ITlsProvider` and passes the
  target's protocols.

## Consequences

- Any connection hook that supplies only a byte stream (vsock, a tunnel) can run curl's TLS over it by
  overriding the two-argument `ConnectorOver`.
- Tests cover the handshake over a fake pipe with a TLS provider that plays recorded handshake bytes.
