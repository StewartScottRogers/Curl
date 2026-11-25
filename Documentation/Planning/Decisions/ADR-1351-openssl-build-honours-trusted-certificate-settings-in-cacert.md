# ADR-1351: The OpenSSL build honours TRUSTED CERTIFICATE settings in --cacert

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5879 (fitness finding STD-198)

## Context

OpenSSL curl reads `-----BEGIN TRUSTED CERTIFICATE-----` blocks in a `--cacert` file: a
certificate followed by `X509_CERT_AUX`, lists of trusted and rejected uses. Curl's
Linux and macOS build ignored them, so a file holding only such blocks was exit 77, and a
rejected root followed by the same root as a plain block was trusted. Fedora and RHEL ship
`ca-bundle.trust.crt` in this format.

Measured with `openssl verify -partial_chain -purpose sslserver` (OpenSSL 3.5) and with
curl 8.18.0 (OpenSSL 3.5.5) on Ubuntu:

- A certificate whose rejected uses name `serverAuth` or `anyExtendedKeyUsage`, or whose
  trusted uses are listed and name neither, fails the chain: exit 60,
  `SSL certificate OpenSSL verify result: certificate rejected (28)`, reported before a
  host name mismatch.
- Of two copies of one certificate the first wins, settings and all.
- The lowest certificate above the server's own that the file lists decides, since curl
  allows partial chains; the server's own settings count only when nothing above it is
  listed.

## Decision

`OpenSslCaFile` reads the OpenSSL build's `--cacert` text (`CERTIFICATE` and `TRUSTED
CERTIFICATE` blocks) with `System.Formats.Asn1`. A rejected certificate stays among the
trust anchors, so the chain is built through it, and
`ServerCertificateVerification` then refuses the chain as above, with verify result 28.
The Schannel build is unchanged: it reads `CERTIFICATE` blocks only.

## Why

Dropping a rejected certificate from the anchors would give OpenSSL's wrong message
(`unable to get local issuer certificate (20)`) and would let a later plain copy trust it.
