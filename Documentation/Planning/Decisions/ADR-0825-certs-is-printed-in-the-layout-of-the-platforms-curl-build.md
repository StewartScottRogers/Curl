# ADR-0825: `%{certs}` is printed in the layout of the platform's curl build

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3620)
- Relates to: [ADR-0054](ADR-0054-the-peer-certificate-chain-is-captured-in-the-tls-provider-and-printed-by-a-port-of-curls-certinfo.md), [ADR-0085](ADR-0085-v-words-a-tls-handshake-as-the-platforms-curl-build-with-openssl-facts-on-the-event.md)

## Context

CGR-091: `-w %{certs}` printed every certificate with `PeerCertificateText`, a port of curl's
`lib/vtls/x509asn1.c`, which is what the Schannel build (Windows) prints. curl's OpenSSL build
(Linux, macOS) does not use x509asn1.c: `ossl_certchain` in `lib/vtls/openssl.c` makes its own
records from OpenSSL's printers. Measured on 2026-10-26 with curl 8.18.0 and OpenSSL 3.5.5 in WSL,
`curl -s -k -o /dev/null -w '%{certs}' https://localhost:<p>/index.txt` against
`openssl s_server -WWW` printed, for each certificate:

- `Subject` and `Issuer` with `XN_FLAG_ONELINE` (`CN = leaf.test, O = "Acme, Inc"`);
- `Version` in hex, `Serial Number` as lower-case hex without colons, `Signature Algorithm`
  and `Public Key Algorithm` by OpenSSL long name;
- one record per extension, named by its long name (`X509v3 Subject Key Identifier`), its
  value as `X509V3_EXT_print` writes it, or the raw bytes with dots for an extension it
  has no printer for (`1.2.3.4:..hi there`); a multi-line value (the authority key
  identifier with an issuer) keeps its line feeds;
- `Start date` and `Expire date` after the extensions, as `ASN1_TIME_print` writes them;
- for an RSA key `RSA Public Key`, `rsa(n)` and `rsa(e)` as `BN_print` writes them
  (upper-case hex, no leading zeros, `10001`); no key records for an EC key;
- `Signature` as lower-case hex pairs each followed by a colon, then the PEM.

## Decision

`PeerCertificateText.FormatFor(certificate, tlsBackend)` prints the x509asn1.c layout for
`TlsBackend.Schannel` and the OpenSSL build's for `TlsBackend.OpenSsl`
(`OpenSslPeerCertificateText`). `TransferWriteOutVariables.TlsBackend` picks the layout and
defaults to `PlatformTlsBackend.ForProcess`, as the `-v` handshake lines do (ADR-0085).

The OpenSSL printers ported are those for the key identifiers, basic constraints, key usage,
extended key usage and the alternative names; every other extension prints its raw value, as
OpenSSL does for one it has no printer for. The rest of OpenSSL's printers (CRL distribution
points, authority information access, certificate policies, CT SCTs and the like) and the DSA
and DH key records are left to a follow-up task.

`CURLINFO_CERTINFO` (`TransferInfoFromReport.CertInfo`) still reads the x509asn1.c layout from
`PeerCertificateText.Format`; moving it with the platform is a separate task too.

## Consequences

On Linux and macOS `%{certs}` matches curl's OpenSSL build for the certificates measured; on
Windows nothing changes. A certificate with an extension OpenSSL prints but this port does not
still differs until the follow-up lands.
