# ADR-0059 — The CONNECT tunnel takes `-A` and the platform credential encoding from the composition, and the proxy from the transfer context

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-267, 2026-09-27).

## Context

BL-212 gave `TcpConnector` an `HttpProxyTunnelOptions(UserAgent, CredentialEncoding)` for
the CONNECT request it sends through an HTTP proxy, and `CurlComposition.CreateTransports`
built the connector without one, so every CONNECT carried `User-Agent: curl/8.21.0` and
UTF-8 `Proxy-Authorization` bytes whatever `-A` said.

BL-267 was filed before ADR-0056 landed and proposed an `IConnector` decorator in
`Curl.Console` that would stamp `ConnectTarget.Proxy` on every connect. ADR-0056 has since
put the proxy on `ITransferContext.Proxy`, chosen per transfer by `TransferProxySelection`,
and every TCP handler copies it into its own `ConnectTarget`.

## Decision

1. `CurlComposition.CreateProxyTunnelOptions` maps the command line to the tunnel's
   options: the `-A` value as the `User-Agent`; `null` (no header) for `-A ""`;
   `curl/8.21.0` without `-A`, matching the header the HTTP handler sends on the request
   itself. The credential encoding is `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())`,
   the one the server credential uses (ADR-0022).
2. `CreateTransports` builds `TcpConnector` with those options and keeps them on
   `CurlTransports.ProxyTunnelOptions`. They are built once per run: the parser does not
   implement `--next`, so `-A` cannot differ between URLs.
3. No proxy decorator is added. The proxy already reaches `ConnectTarget.Proxy` through
   the transfer context (ADR-0056), per transfer and after `--noproxy`, the environment
   variables and the `-U` override; a decorator stamping one run-wide proxy would bypass
   all three.

## Consequences

- `-A` and the platform encoding now reach the CONNECT bytes, pinned in
  `CurlTransportsTests` and `CurlCompositionProxyTests`.
- When `--next` is implemented, the tunnel options must be built per URL group, as the
  TLS options must.

## Alternatives considered

- **The decorator from the task text.** Rejected: it duplicates ADR-0056's route and would
  ignore `--noproxy` and the proxy environment variables.
- **Carrying the `User-Agent` on `ConnectTarget`.** Rejected: it changes the shared
  contract in `Curl.Protocol.Abstractions.UnitLibrary` for a value that is fixed per run.
