# ADR-0205 — The hand-built TLS client offers TLS 1.3 and TLS 1.2 in one ClientHello and continues on the version the ServerHello picks

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-821.
Builds on ADR-0140 ("Class structure" names `TlsClientConnection`), ADR-0157 (TLS 1.3 over a
byte stream), ADR-0249 (TLS 1.2 and below over a byte stream) and ADR-0162 (which client
`HandBuiltTlsProvider` runs for a version range).

## Context

Until now `HandBuiltTlsProvider` ran `Tls13ClientConnection` for any range reaching TLS 1.3
(ADR-0162 decision 3), so `--tlsv1.2` with no ceiling offered TLS 1.3 alone and could not
reach a TLS 1.2 server. Both curl builds offer every version in the range in one hello and
go on as whichever the server picks. The two hand-built handshakes are separate state
machines, each building its own ClientHello and transcript.

## Decision

- **One hello, built by the TLS 1.3 client.** `TlsClientSettings` pairs a
  `Tls13ClientSettings` with a `Tls12ClientSettings`. The hello is the TLS 1.3 one, with the
  TLS 1.2 half added through the internal `Tls13ClientSettings.LowerVersions`: its versions
  after `0x0304` in `supported_versions` (ceiling down to minimum), its suites after the
  TLS 1.3 ones, its signature schemes after the TLS 1.3 list (the `rsa_pkcs1_*` and
  `ecdsa_sha1` schemes TLS 1.2 needs), and its extensions whose types the TLS 1.3 extension
  order and fixed extensions do not name (`renegotiation_info`, `ec_point_formats`,
  `session_ticket`, `encrypt_then_mac`, `extended_master_secret`) after the listed ones.
  The TLS 1.3 handshake still checks a CertificateVerify against its own list, so a
  `rsa_pkcs1_*` CertificateVerify stays refused.
- **The TLS 1.2 half has a TLS 1.2 ceiling and no session to resume.** The hello's
  `legacy_version` is TLS 1.2 (which the RSA pre-master secret must repeat), and its session
  ID is TLS 1.3's, so resumption by session ID cannot be offered in it. Either is an
  `ArgumentException`.
- **The version is read before either handshake sees the ServerHello.**
  `TlsClientConnection.ConnectAsync` sends the hello through `Tls13ClientConnection`, then
  reads plaintext handshake records through the internal `ServerHelloReplayStream`, which
  keeps what it reads, until the first handshake message is whole. A ServerHello without
  `supported_versions` goes to `Tls12ClientHandshake.StartFrom(sent)`, which takes the sent
  hello into its transcript and runs on; anything else (a TLS 1.3 ServerHello or
  HelloRetryRequest, an alert, a malformed or oversized record, a closed transport) goes to
  the TLS 1.3 client, which fails it exactly as it fails it after a TLS 1.3-only hello.
  Either way the stream then replays the kept bytes, so the chosen record layer reads the
  server's records from the first byte. This keeps both state machines unchanged in shape:
  no record is parsed twice by different rules.
- **Downgrade sentinels (RFC 8446 section 4.1.3).** A TLS 1.2 handshake started from a hello
  that offered TLS 1.3 refuses a ServerHello random ending in `DOWNGRD\x01` or `DOWNGRD\x00`
  with `illegal_parameter`; one started on its own keeps the TLS 1.2 client's rule
  (`DOWNGRD\x00` below TLS 1.2 with a TLS 1.2 ceiling).
- **`TlsConnectResult`** carries a `Tls13ClientStream` or a `Tls12ClientStream`, not a common
  base: the two streams expose different handshakes, and the provider already describes each.
- **Which client `HandBuiltTlsProvider` runs.** A range reaching TLS 1.3 whose minimum is below
  it runs `TlsClientConnection`; a TLS 1.3 minimum runs `Tls13ClientConnection`; a ceiling below
  TLS 1.3 runs `Tls12ClientConnection` (ADR-0162, unchanged). When `--ciphers` and
  `--tls13-ciphers` leave no suite the client can run for one side, the other side runs alone
  rather than offering a version with no suite.
- **The default minimum beside TLS 1.3 is TLS 1.2.** With no `--tlsv1.x` and no ceiling below
  TLS 1.3, the TLS 1.2 half offers TLS 1.2 only: curl's default minimum is TLS 1.2 since
  8.10.0 on every TLS backend. ADR-0162's TLS 1.0 floor for an unset minimum under a TLS 1.2,
  1.1 or 1.0 ceiling is unchanged.

## Consequences

- `--tlsv1.2` (and the default range) through the hand-built client now reaches a TLS 1.2-only
  server; `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_Tls12MinimumWithNoCeilingAgainstATls12OnlyServer_ConnectsOverTls12`
  shows it against a server-side `SslStream`.
- The hello is not yet any measured build's byte for byte; `ClientHelloProfile` stays the
  measured reference, and pinning the hand-built TCP hello to it is separate work.
