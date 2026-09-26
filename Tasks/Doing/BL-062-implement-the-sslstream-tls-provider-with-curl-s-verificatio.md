---
id: BL-062
title: Implement the SslStream TLS provider with curl's verification, -k and TLS minimum versions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-061]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-062 — Implement the SslStream TLS provider with curl's verification, -k and TLS minimum versions

## Goal

`SslStreamTlsProvider` in `Curl.Networking.UnitLibrary` is the production
`ITlsProvider`: it runs a client handshake with `System.Net.Security.SslStream` over the
plaintext `IConnection`, verifies the server certificate and host name as curl does by
default, skips verification under `-k`, enforces a `--tlsv1.2`/`--tlsv1.3` minimum, and
returns exit 60 for a verification failure and exit 35 for any other handshake failure.

## Context

- ADR-0005: TLS lives in `Curl.Networking.UnitLibrary`, behind `ITlsProvider`, used by
  `TcpConnector` when `ConnectTarget.UseTls` is set. `Curl.Networking.UnitLibrary/CLAUDE.md`:
  "The TLS provider, when it lands, is the only type that may construct an `SslStream`."
  Product Overview, Rule 2: TLS is .NET's `SslStream`, never a hand-written handshake.
  BCL only (`System.Net.Security`, `System.Security.Cryptography.X509Certificates`); no
  package.
- BL-061 changes `ITlsProvider` to return `ConnectResult`. This task depends on it.
- Settings come in through the constructor, not through `ITransferContext`: add
  `TlsClientOptions`, a sealed record in `Curl.Networking.UnitLibrary`, with
  `bool Insecure` (default `false`) and a minimum TLS version (default: the operating
  system's choice; `Tls12` means 1.2 or later; `Tls13` means 1.3 only). BL-063, BL-064,
  BL-065 and BL-066 add further members; `Curl.Console` builds it from the command line
  (BL-071).
- Upstream (<https://curl.se/docs/manpage.html>, as published for curl 8.23.0 on
  2026-09-26): `-k` "tells curl to skip the verification step and proceed without
  checking"; `--tlsv1.2` "means version 1.2 or later"; `--tlsv1.3` "means version 1.3
  or later". Exit codes (<https://curl.se/libcurl/c/libcurl-errors.html>): 35
  `CURLE_SSL_CONNECT_ERROR`, 60 `CURLE_PEER_FAILED_VERIFICATION`; both already exist on
  `CurlExitCode` as `SslConnectError` and `PeerFailedVerification`.
- Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel,
  Release-Date 2026-06-24) against `openssl s_server` on loopback with a self-signed
  `CN=localhost` certificate:
  - untrusted certificate, no `-k`: exit 60;
  - `https://127.0.0.1:…` where the certificate names only `localhost`: exit 60;
  - the same two with `-k`: exit 0, body delivered;
  - `--tlsv1.3 -k` against a server limited to TLS 1.2: exit 35;
  - `--tlsv1.2 -k` against that server: exit 0.
- The message text for 35 and 60 differs between curl's TLS builds and is Stewart's
  decision (BL-059); BL-064 sets it. Here, build every failure message in one internal
  place so BL-064 changes only that place, and do not assert message text in tests.

Testing without a network: run the handshake between this provider and a server-side
`SslStream` inside the test over a hand-written in-memory duplex stream pair (in
`Curl.Networking.UnitTests/Fakes`), with certificates made in the test by
`CertificateRequest.CreateSelfSigned`. On Windows, a server certificate whose key is
ephemeral may fail the handshake; exporting it to PKCS#12 and reloading it with
`X509CertificateLoader.LoadPkcs12` is the usual remedy. Verification against the
system store is exercised by a certificate the system cannot trust (self-signed), not
by adding anything to the machine's store.

## Acceptance criteria

- [ ] `SslStreamTlsProvider(TlsClientOptions options)` implements `ITlsProvider`; it is
      the only type in the solution that constructs an `SslStream`, and it passes the
      target host to the handshake for server name indication and name checking.
- [ ] A successful handshake returns `ConnectResult.Connected` with an `IConnection`
      whose `IsSecure` is `true`, whose `RemoteEndPoint` is the plaintext connection's,
      and whose reads and writes go through the `SslStream`; a test round-trips bytes
      to the in-memory server.
- [ ] Named tests assert exit 60 (`CurlExitCode.PeerFailedVerification`) for an
      untrusted self-signed server certificate and for a certificate whose names do not
      match the target host, without `Insecure`.
- [ ] Named tests assert that with `Insecure = true` both of those handshakes succeed.
- [ ] Named tests assert exit 35 (`CurlExitCode.SslConnectError`) when the minimum is
      `Tls13` and the in-memory server allows only TLS 1.2, and when the server closes
      the stream mid-handshake; and success when the minimum is `Tls12` against that
      server. Where the test host's operating system cannot offer TLS 1.3, the TLS 1.3
      test reports `Assert.Inconclusive` with the reason instead of passing silently.
- [ ] On every failed result the plaintext connection has been disposed; a test asserts
      it.
- [ ] A cancelled token surfaces as `OperationCanceledException`; no other exception
      escapes `AuthenticateAsClientAsync`.
- [ ] No test is tagged `TestCategory("Integration")` and no test opens a socket.
- [ ] `Curl.Networking.UnitLibrary/CLAUDE.md` names `SslStreamTlsProvider` as the type
      that constructs `SslStream`.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

`--cacert` (BL-063), `--capath` and message text (BL-064), `--cert`/`--key` (BL-065)
and `--ciphers` (BL-066) are separate tasks. `--tls-max` and `--tlsv1.0`/`--tlsv1.1` are
not planned yet.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
