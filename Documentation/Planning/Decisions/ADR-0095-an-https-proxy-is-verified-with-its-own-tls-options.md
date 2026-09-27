# ADR-0095 — An HTTPS proxy is verified with its own TLS options

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-362, 2026-09-27). Supersedes item 2
of ADR-0061.

## Context

ADR-0061 ran the handshake to an HTTPS proxy through the transfer's `ITlsProvider`, so
`-k`, `--cacert` and `--capath` reached the proxy, which curl does not do. curl 8.21.0
(the Schannel reference build, ADR-0009) was measured against a TLS loopback proxy with a
self-signed `CN=localhost` certificate (commands in BL-362's Notes):

- `-k` and `--cacert <the proxy's certificate>` both fail with exit 60
  `schannel: SEC_E_UNTRUSTED_ROOT ...`: neither reaches the proxy.
- `--proxy-insecure` and `--proxy-cacert <the proxy's certificate>` both reach CONNECT
  (`curl: (7) CONNECT tunnel failed, response 407`); `--no-proxy-insecure` turns it off.
- `--proxy-cacert` with a missing file is refused exactly as `--cacert` is, naming
  `--proxy-cacert`; `--proxy-capath ''` is a blank argument, and a flag-like
  `--proxy-capath` value gets the filename warning.
- `--capath`, `--proxy-capath` or both print one `Warning: ignoring setting the CA path for
  the proxy, not supported by libcurl with Schannel`, with or without a proxy: curl's tool
  sets the proxy CA path from `--proxy-capath`, falling back to `--capath`.

## Decision

1. `Curl.Cli` parses `--proxy-insecure` (negatable), `--proxy-cacert` (checked as
   `--cacert` is) and `--proxy-capath` into `ProxyInsecure`, `ProxyCaCertificateFile` and
   `ProxyCaCertificateDirectory`. The rest of the `--proxy-*` TLS family (`--proxy-cert`,
   `--proxy-key`, `--proxy-ciphers`, `--proxy-tlsv1` and so on) is left for later tasks:
   the goal is verifying the proxy, and those options present or negotiate, not verify.
2. `TcpConnector` takes an optional `proxyTlsProvider`, defaulting to `tlsProvider`, and
   runs the handshake to an HTTPS proxy through it.
3. `Curl.Console` builds a second `SslStreamTlsProvider` from
   `TlsClientOptionsMapping.ProxyFromCommandLine`: `Insecure` from `--proxy-insecure`,
   `CaCertificateFile` from `--proxy-cacert`, `CaCertificateDirectory` from
   `--proxy-capath` or else `--capath` (curl's fallback), every other setting its default,
   so the proxy verifies against the system store unless a `--proxy-*` option says
   otherwise.
4. The warning lines printed before each transfer are the proxy provider's `Warnings`, not
   the target's: its CA path is set whenever `--capath` or `--proxy-capath` is, and the
   Schannel warning is about the proxy, so it is printed once for either or both, as
   measured.

## Consequences

- `-k` and `--cacert` no longer reach an HTTPS proxy, as in curl.
- An HTTPS proxy is still refused by the console with exit 4 for an `https` target or under
  `-p` or `-L` until BL-328 lifts it; the wiring is ready for it.
- `CurlCompositionTests` reads `TcpConnector`'s two `ITlsProvider` fields by name.

## Alternatives considered

- Copying the target's TLS options to the proxy unless a `--proxy-*` option is given: curl
  does not, as measured.
- Printing both providers' warnings: the one Schannel warning would be printed twice.
