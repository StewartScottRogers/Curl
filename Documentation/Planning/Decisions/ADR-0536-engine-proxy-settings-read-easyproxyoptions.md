# ADR-0536 — The engine's proxy settings read `EasyProxyOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2325.

## Context

ADR-0515 built `EasyOptionsFromCommandLine.ProxyOf` and settled the proxy mapping. BL-2325 has
`TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`,
`Socks5AuthenticationMapping`, `TransferPerformer`'s proxy selection and `TransferContextFactory`'s `-p`
and `--suppress-connect-headers` read it in place of `CommandLineOptions`, with behaviour unchanged. The
record holds the proxy as text and a `ProxyType`, where the command line held `CommandLineProxy`'s address
and kind, and folds `--proxy-http2` into `ProxyType.Https2`.

## Decision

- **`ProxyType` is the kind of proxy text with no scheme.** `Http`, `Http10`, `Https` and the SOCKS types map
  to the same `ProxyKind`; a scheme in the text still wins, and `Http10` still keeps an `http://` proxy at
  HTTP/1.0 (BL-1855).
- **`Https2` stays HTTP and asks for HTTP/2.** `ProxyOf` gives `Https2` only for `-x`'s HTTP under
  `--proxy-http2`, which Curl has always spoken to as an HTTP proxy, asking only an `https://` proxy's tunnel
  for HTTP/2. So `Https2` is `ProxyKind.Http` for selection and `HttpProxyTunnelOptions.ProxyHttp2` is
  `ProxyType == Https2`. `--proxy-http2` with no `-x` (an environment proxy) or with a `--socks*` or
  `--proxy1.0` proxy no longer reaches the tunnel: curl's tool sets `CURLOPT_PROXYTYPE` only with `-x`, so
  this follows the record as ADR-0515 defines it.
- **SOCKS5 methods are the record's bits.** `Socks5AuthenticationMapping.Of` allows each method whose bit is
  set; `ProxyOf` gives libcurl's `Basic | GssApi` when neither `--socks5-basic` nor `--socks5-gssapi` is
  given, which is what "neither" allowed before.
- **Authentication and network settings come in beside the record.** `-U`, `--proxy-service-name` and
  `--delegation` are the authentication group's: `TransferProxySelection.TrySelect` takes the `-U`
  credential, `Socks5AuthenticationMapping.Of` the `NegotiateOptions`. Whether `--unix-socket` is given comes
  in as a flag. `TransferContextFactory.ConnectToTunnelsThroughProxy` reads only `--connect-to`, a network
  setting, and is left to BL-2343. `HttpRequestOptionsMapping`'s `--proxy-header` copy is not one of the
  named members and stays for the HTTP group's retarget.

## Consequences

`CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector`, `OptionGroupConnectionSettings.Of`,
`TransferContextFactory.Create` and `TransferPerformer.TrySelectProxyAndCredentials` take an
`EasyProxyOptions` parameter, which `Curl.Console` builds with `EasyOptionsFromCommandLine.ProxyOf`.
`Socks5AuthenticationMapping.FromCommandLine` is now `Of`.
