# ADR-0047 — The peer certificate chain is captured in the TLS provider and printed by a port of curl's certinfo

- **Status:** Accepted
- **Date:** 2026-09-26
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

`%{num_certs}` and `%{certs}` were left unknown by ADR-0043 because nothing carried the
server's certificates out of the TLS handshake (BL-303). curl 8.21.0 (x86_64-w64-mingw32,
Schannel), measured on 2026-09-26:

- file:// and http:// print `0` and nothing (BL-284's Notes).
- A loopback `openssl s_server` sending a leaf, an intermediate and a root, with
  `curl -k -s -o NUL -w "%{num_certs}\n%{certs}" https://127.0.0.1:18304/`, printed `3`
  and one block per certificate, in the order sent, server's first. The bytes are pinned
  in `Curl.Output.UnitTests/Fixtures/LoopbackChain.certs.txt`; BL-303's Notes hold the
  commands. `https://example.com/` printed `4`, exactly the four certificates
  `openssl s_client -showcerts` shows the server sending, in that order.
- The chain is gathered whether or not the certificate is verified (`-k` prints it) and
  without `--certinfo`: the tool turns certinfo on when `-w` names either variable.

Each block is what `Curl_extract_certinfo` in `lib/vtls/x509asn1.c` builds from the DER
bytes, not what Windows or OpenSSL would print: `Subject`, `Issuer`, `Version`,
`Serial Number`, `Signature Algorithm`, `Start Date`, `Expire Date`,
`Public Key Algorithm`, the key's records, `Signature`, and the PEM. Its details are its
own: a small serial prints in decimal (`7`) and a larger one as `0x7fff`, the version in
hex (`2` for v3), distinguished names in encoded order with `, ` or `/` between attributes,
curl's own short OID table (so `UID` prints as `0.9.2342.19200300.100.1.1`), and the
Schannel build lists the certificates the server sent (`traverse_cert_store` over the
remote certificate's store), not the chain Windows builds.

## Decision

1. **Where the chain is captured.** `SslStreamTlsProvider` takes it in its
   `RemoteCertificateValidationCallback`, the one place `SslStream` shows what the server
   sent: the server's certificate, then the chain's `ChainPolicy.ExtraStore`, which .NET
   fills with the other certificates the server sent, in the order sent (checked against
   example.com and the loopback server on Windows). A copy of the server's certificate
   in the extra store is not listed twice.
2. **How it reaches `TransferReport`.** As DER bytes, never as `X509Certificate2`, so the
   contracts stay plain data: `ConnectResult.PeerCertificates`
   (`IReadOnlyList<ReadOnlyMemory<byte>>`, empty without TLS), carried through by
   `TcpConnector`, and `TransferReport.PeerCertificates`, which each handler copies from
   the `ConnectResult` it connected with, as it copies `LocalEndPoint`. The HTTP handler's
   copy is BL-314; this task could not touch `Curl.Protocol.Http.UnitLibrary` while BL-180
   held it. Every later TLS handler copies it the same way.
3. **How it is printed.** `Curl.Output.PeerCertificateText` is a port of
   `Curl_extract_certinfo` and the parser under it (`DerReader` for `getASN1Element`,
   `X509CertificateFields` for `Curl_parseX509`, `DerText` for `ASN1tostr` and
   `encodeDN`), with curl's leniencies and refusals. `%{num_certs}` is the list's count and
   `%{certs}` the blocks one after another. It is written by hand on the base class
   library; `System.Formats.Asn1` would refuse encodings curl accepts. The ported files
   carry curl's copyright line and name the curl licence, which asks for that notice in
   copies of substantial portions.
4. **Where the port cannot be curl.** A certificate curl cannot parse makes curl fail the
   handshake ("Failed extracting certificate chain"). Here the transfer has finished before
   `-w` is printed, and `SslStream` has already accepted the certificate, so such a
   certificate prints nothing and is still counted. A character a .NET string cannot hold
   (a lone surrogate, a code point past U+10FFFF, invalid UTF-8) prints as U+FFFD where curl
   writes its raw bytes. The Schannel build's refusal of more than 100 certificates (exit
   35) is not reproduced. The DSA and Diffie-Hellman records, and the refusals, follow the
   source; Schannel would not negotiate a DSA server certificate to measure them.

## Consequences

- `-w "%{certs}"` matches curl byte for byte for the measured chain, and for any
  certificate the port reads as curl does, with no dependency beyond the base class library.
- The chain is captured on every TLS connection, whether or not `-w` asks for it; it is a
  few kilobytes of bytes already in memory.
- Each TLS handler must copy the chain into its report; until BL-314, https:// prints `0`
  and nothing.
- The OpenSSL build prints the same records through `Curl_ossl_certchain`, which uses
  OpenSSL's own printers and differs in detail. This port matches the Schannel build, the
  one ADR-0009 names for Windows; the OpenSSL build's text is not measured or reproduced.

## Alternatives considered

- **Carry `X509Certificate2` objects.** Lost: it puts a disposable, platform-backed type
  in the contracts, and the printer needs only the DER bytes curl itself reads.
- **Print from `X509Certificate2` (`Subject`, `SerialNumber`, `PublicKey`).** Lost: .NET
  formats names in reverse order with its own attribute names and serials in upper-case hex,
  so none of the measured lines would match.
- **Use the chain `X509Chain` builds.** Lost: it ends at a trusted root the server never
  sent and drops what the server sent that it does not need; curl lists what was sent.
- **Leave the variables unknown.** Lost: curl knows them, and a script that reads them
  must not see the unknown-variable warning.
