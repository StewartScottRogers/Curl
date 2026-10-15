# ADR-0498: The HTTP/3 stand-in takes datagrams on port 9019, and the http/3 cases run off Windows only

- Status: Accepted
- Date: 2026-10-14
- Decided by Claude under Stewart's delegation (BL-2261)

## Context

BL-2231 built the stand-in for upstream's `http/3` test server, nghttpx-h3 relaying HTTP/3 over QUIC
to `sws` (`Curl.Conformance.Http3Server.UnitLibrary`), and BL-2260 sends in-process Curl's QUIC
through the injected datagram connector. BL-2261 puts it on `%HTTP3PORT`. Four choices came up:

- `servers.pm` picks a free port for `%HTTP3PORT`; the harness needs a fixed one.
- The stand-in presents `%CERTDIR/certs/test-localhost.pem`, which the harness's generator writes
  with a P-256 key, not the RSA key `Http3ServerTlsSettings.FromRsaCertificate` takes.
- Upstream's test2500 sends `--http3`, which races a TCP connection against QUIC. Every in-memory
  TCP connection the harness does not route elsewhere reaches the `sws` emulation, so the TCP leg
  reached `sws` on port 9019, won the race and sent it a TLS ClientHello (measured on this lane).
  The case expects `HTTP/3 200`, so upstream's race settles on QUIC.
- Curl's Windows build matches curl's Schannel build, which refuses `--http3` (`RefusedBySchannelBuild`,
  measured on this lane: `curl: option --http3: the installed libcurl version does not support this`),
  yet `UpstreamCurlPlatform` reported `http/3` on every platform.

## Decision

- `%HTTP3PORT` is 9019 (`UpstreamCaseRunner.Http3Port`, `UpstreamHttp3Server.Http3Port`), the next
  port no other stand-in uses, and has a value only when the caller gives an `UpstreamHttp3Server`
  and names a certificate directory, as the stunnel-fronted ports do.
- `UpstreamHttp3Server` is a record the caller builds from the stand-in's library and passes as
  `UpstreamCaseRunner`'s optional `http3Server`, as it does `UpstreamHttp2Server` (ADR-0495). Given
  one, the platform reports `nghttpx-h3`, screening lets a case naming the `http/3` server run, and
  for such a case datagram opens to 9019 reach a fresh stand-in relaying to the case's `sws`
  emulation; every other datagram reaches the dnsd and tftpd stand-ins as before.
- The conformance tests build the stand-in's TLS settings themselves, signing with the certificate's
  ECDSA key (`EcdsaTlsSigningKey`); the stand-in's own transport parameters replace the ones given.
- With an `UpstreamHttp3Server` given, a TCP connection to 9019 is refused
  (`UpstreamHttp3Server.RefusingTcpOnHttp3Port`), so `--http3`'s race settles on QUIC, as upstream's
  expected output requires.
- `UpstreamCurlPlatform` reports `http/3` on Linux and macOS only, as ADR-0495 does for `http/2`: on
  Windows the `http/3` cases are `SkippedOnThisPlatform`, and CI's Linux and macOS jobs measure them.

## Consequences

- test2500 is listed. On Windows it is `Inconclusive`. Emulating the Linux build on Windows (parsing
  as the non-Windows build, the Linux feature set, and `--ssl-no-revoke` in place of the Windows
  chain's revocation check, which the OpenSSL build does not make) it passes over `--http3` and
  `--http3-only` alike.
- 2501 stays screened for `Debug`, 2502 for its `<tool>` (`lib2502`, a libcurl multi program) and 2503
  for `headers-api`, none of them for the server.
