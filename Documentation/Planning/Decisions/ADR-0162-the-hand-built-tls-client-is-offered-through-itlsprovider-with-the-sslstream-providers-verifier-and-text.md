# ADR-0162 — The hand-built TLS client is offered through `ITlsProvider` with the `SslStream` provider's verifier and text

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-708.

## Context

ADR-0140 routes a connection's TLS to the hand-built client in `Curl.Tls.UnitLibrary` only
when an option `SslStream` cannot honour is in force, and gives BL-708 the second
`ITlsProvider`, the routing function, the verifier shared with `SslStreamTlsProvider`, and
the failure mapping. When BL-708 ran, `Curl.Tls.UnitLibrary` had two connections over a
byte stream, `Tls13ClientConnection` (ADR-0157) and `Tls12ClientConnection` (ADR-0254),
but no connection that offers TLS 1.3 and TLS 1.2 in one ClientHello and picks by the
ServerHello, and no ClientHello profiles (BL-787). Of ADR-0140's routing rows, only the
legacy-versions row's option (`--tls-max 1.0` or `1.1`) reaches `TlsClientOptions`; the
others arrive with BL-618, BL-610 and BL-713.

A spike on 2026-09-28 ran both connections against a Windows 11 `SslStream` (Schannel)
server over loopback: TLS 1.2 negotiated `TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384`, TLS 1.3
`TLS_AES_256_GCM_SHA384`, and application data round-tripped.

## Decision

1. **Routing.** `TlsClientRouting.Choose(TlsClientOptions)` is one pure function returning
   `TlsClientRoute.SslStream` or `HandBuilt`, with one condition per ADR-0140 row whose
   option exists: today a ceiling of TLS 1.0 or 1.1. Each later option task adds its row
   and a data row in `TlsClientRoutingTests`. `CurlComposition.CreateTlsProvider` builds
   `HandBuiltTlsProvider` or `SslStreamTlsProvider` from it, for the origin's options and
   the HTTPS proxy's alike; both implement `ITlsProviderWithWarnings`, so the console
   prints the build's warnings whichever runs.
2. **One verifier.** The chain judgement moves out of `SslStreamTlsProvider` into
   `ServerCertificateVerification` (trust anchors, tolerated chain errors, each build's
   name check, the OpenSSL verify result, exit 60 and 77 and their text). The `SslStream`
   provider calls it from its validation callback; `HandBuiltCertificateVerifier`, the
   hand-built client's `IServerCertificateVerifier`, builds the `X509Chain` `SslStream`
   would build (the `--cacert` policy, or the system store for server authentication with
   no revocation check, the other certificates sent as candidates), computes the
   `SslPolicyErrors` it would pass (`RemoteCertificateNotAvailable` for no certificate or
   an unparsable one, `RemoteCertificateChainErrors` when the chain does not build,
   `RemoteCertificateNameMismatch` from `X509Certificate2.MatchesHostname`), and calls the
   same judgement. The rejection travels back in `TlsHandshakeFailure.CertificateRejection`
   as the exit code and message.
3. **Which connection.** A range that reaches TLS 1.3 runs `Tls13ClientConnection`; a
   ceiling of TLS 1.2, 1.1 or 1.0 runs `Tls12ClientConnection` from the minimum (TLS 1.0
   when none is given, as `TlsVersionRange` offers it) to the ceiling. A range such as
   `--tlsv1.2` alone therefore offers only TLS 1.3 on the hand-built route; no row routes
   such a range today, and one ClientHello offering both is filed as follow-up work.
4. **What the hello carries.** `server_name` with the target host unless it is an IP
   address (RFC 6066 section 3), ALPN unless `--no-alpn`, the `Curl.Tls` default suites,
   groups and signature algorithms until BL-787's profiles land, and the `--cert`
   certificate, loaded exactly as the `SslStream` provider loads it
   (`ClientCertificateLoader.Load`: same exits 58 and 43), when its key is RSA or ECDSA.
   `--ciphers` and `--tls13-ciphers` follow ADR-0011: the Schannel build refuses
   `--ciphers` with exit 59; the OpenSSL build offers the named suites the connection can
   protect, and a list naming none of them is exit 59 with `OpenSslCipherSuites.Unapplied`.
5. **Failures and text.** A certificate rejection is the shared verifier's exit and text.
   Any other handshake failure is exit 35: the Schannel build prints `failed to receive
   handshake` for a transport that closed and, as BL-502 measured, for any ceiling below
   TLS 1.2, and otherwise ADR-0140's measured fatal-alert line; the OpenSSL build prints
   OpenSSL's unexpected-EOF string for a close and, for an alert either side sent,
   `error:0A000<1000 + alert>:SSL routines::<reason>` with OpenSSL 3's reason string (the
   handshake-failure and protocol-version strings are measured; the rest are OpenSSL's
   `ssl_err.c` names, and `reason(N)` where it has none). A transport exception is the
   `SslStream` provider's text for it (`Recv failure: ...`). BL-714 and the option tasks
   pin each case they meet against real curl.
6. **Events.** The trust event and the handshake event are the `SslStream` provider's:
   the negotiated version and suite, ALPN offered and selected, the server's certificate,
   the verify result and chain, `IsProxy` and the verified host name. The key-exchange
   group and peer signature type stay `null`, as on the `SslStream` path, so `-v` reads
   the same on both.
7. **A stream that ends without `close_notify`.** `HandBuiltTlsConnection` returns 0 at the
   server's `close_notify` and at a bare transport end alike, as `SslStreamConnection`
   does, so the two paths behave the same today. ADR-0157 leaves exit 56 for an unfinished
   transfer to the caller; curl fails such a read on both builds, and Curl's `SslStream`
   path does not either, so both are filed as one follow-up task that carries a typed
   read failure to the protocol handlers.

## Consequences

- `curl --tls-max 1.0 https://...` and `--tls-max 1.1` now run the hand-built TLS 1.2/1.1/1.0
  client on every platform, where the operating system's stack refused them; every other
  option set keeps `SslStream` and its bytes.
- `Curl.Networking.UnitLibrary` references `Curl.Tls.UnitLibrary`, as ADR-0120 allows.
- `SslStreamTlsProvider` keeps its behaviour; its verification code now lives in
  `ServerCertificateVerification`, and its `--cert` loading in `ClientCertificateLoader.Load`.
- Follow-up work: one ClientHello offering TLS 1.3 and TLS 1.2 on the hand-built route;
  exit 56 for a TLS stream that ends without `close_notify` on both paths; the platform's
  ClientHello profile once BL-787 builds the profiles.

## Alternatives considered

- **A routing provider that holds both clients and chooses per handshake.** The options are
  fixed for a provider's life, so choosing once when the provider is built is the same
  answer with less code. Rejected.
- **Fail a bare transport end with exit 56 inside `HandBuiltTlsConnection` now.** The HTTP
  handler would report it as `Recv failure: ...`, not curl's text, and the hand-built path
  would then differ from the `SslStream` path. Rejected for the follow-up that does both.
- **Refuse `--ciphers` on the hand-built route until the profiles land.** Would make
  `--tls-max 1.0 --ciphers X` fail where the OpenSSL build honours it. Rejected.
