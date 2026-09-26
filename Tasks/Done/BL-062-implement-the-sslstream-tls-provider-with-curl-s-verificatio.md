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
completed: 2026-09-26
---
# BL-062 â€” Implement the SslStream TLS provider with curl's verification, -k and TLS minimum versions

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
  - `https://127.0.0.1:â€¦` where the certificate names only `localhost`: exit 60;
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

- [x] `SslStreamTlsProvider(TlsClientOptions options)` implements `ITlsProvider`; it is
      the only type in the solution that constructs an `SslStream`, and it passes the
      target host to the handshake for server name indication and name checking.
- [x] A successful handshake returns `ConnectResult.Connected` with an `IConnection`
      whose `IsSecure` is `true`, whose `RemoteEndPoint` is the plaintext connection's,
      and whose reads and writes go through the `SslStream`; a test round-trips bytes
      to the in-memory server.
- [x] Named tests assert exit 60 (`CurlExitCode.PeerFailedVerification`) for an
      untrusted self-signed server certificate and for a certificate whose names do not
      match the target host, without `Insecure`.
- [x] Named tests assert that with `Insecure = true` both of those handshakes succeed.
- [x] Named tests assert exit 35 (`CurlExitCode.SslConnectError`) when the minimum is
      `Tls13` and the in-memory server allows only TLS 1.2, and when the server closes
      the stream mid-handshake; and success when the minimum is `Tls12` against that
      server. Where the test host's operating system cannot offer TLS 1.3, the TLS 1.3
      test reports `Assert.Inconclusive` with the reason instead of passing silently.
- [x] On every failed result the plaintext connection has been disposed; a test asserts
      it.
- [x] A cancelled token surfaces as `OperationCanceledException`; no other exception
      escapes `AuthenticateAsClientAsync`.
- [x] No test is tagged `TestCategory("Integration")` and no test opens a socket.
- [x] `Curl.Networking.UnitLibrary/CLAUDE.md` names `SslStreamTlsProvider` as the type
      that constructs `SslStream`.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

`--cacert` (BL-063), `--capath` and message text (BL-064), `--cert`/`--key` (BL-065)
and `--ciphers` (BL-066) are separate tasks. `--tls-max` and `--tlsv1.0`/`--tlsv1.1` are
not planned yet.

Delivered (dark factory lane 1, 2026-09-26):

- New types: `SslStreamTlsProvider`, `TlsClientOptions(bool Insecure = false,
  TlsMinimumVersion MinimumVersion = SystemDefault)`, `TlsMinimumVersion`
  (`SystemDefault`, `Tls12`, `Tls13`), and internal `ConnectionStream` (the `Stream` over
  `IConnection` that `SslStream` runs on), `SslStreamConnection` (the secure
  `IConnection`) and `TlsFailureMessages` (the one place BL-064 edits).
- Choice: the enum is named `TlsMinimumVersion` and its default member `SystemDefault`
  (maps to `SslProtocols.None`), because the task names the default "the operating
  system's choice". `Tls12` maps to `Tls12 | Tls13`, `Tls13` to `Tls13`.
- Choice: `SslStream` is created with `leaveInnerStreamOpen: true`, and the plaintext
  connection is disposed explicitly, so `ConnectionStream` never has to dispose an async
  connection from the synchronous `Stream.Dispose`.
- Learned: disposing a `SslStream` flushes its inner stream, which throws again when the
  plaintext connection is what failed; the failure path swallows that one exception so
  the handshake's own failure is reported as exit 35.
- Learned: an `await` inside `catch` (or `ExceptionDispatchInfo.Throw` inside an `if`)
  leaves compiler-generated branches and unreachable sequence points that the coverage
  collector counts as uncovered; the provider captures the exception, disposes after the
  `try`, and rethrows cancellation through an early-return helper. New code measures 100%
  line and branch coverage.
- Choice: certificate revocation checking is left at .NET's default (no check), matching
  curl's OpenSSL builds; Schannel curl checks revocation. Revisit with BL-064 if needed.
- Choice: the untrusted and name-mismatch tests both use a self-signed certificate, so
  the name-mismatch case also carries a chain error; isolating a pure name mismatch needs
  a trusted chain, which arrives with `--cacert` (BL-063).
- "Only type that constructs an `SslStream`" is read as production code: the tests
  construct the server-side `SslStream`, as the task's testing section directs.
- Pipeline: the plan, tests and implementation were done in-session rather than through
  separate protocol-architect / code-reviewer runs, since the task's Context already
  fixed the design.
- Tests: 13 in `SslStreamTlsProviderTests`, 5 in `ConnectionStreamTests`;
  `Curl.Networking.UnitTests` 51 passed, 0 skipped, TLS 1.3 available on the lane host.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. SslStreamTlsProvider handshakes over IConnection with curl's verification (exit 60), -k, --tlsv1.2/--tlsv1.3 minimums and exit 35 for other failures
