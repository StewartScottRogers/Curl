# ADR-0197 — `--crlfile` is checked by hand in the OpenSSL build and ignored by the Schannel build

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-609.

## Context

BL-607 parses `--crlfile` into `CommandLineOptions.CertificateRevocationListFile`, refusing a
path at which nothing exists with exit 2. curl's OpenSSL backend loads the file with
`X509_load_crl_file(..., X509_FILETYPE_PEM)` and sets `X509_V_FLAG_CRL_CHECK |
X509_V_FLAG_CRL_CHECK_ALL`, so every certificate of the chain, the root included, needs a list
from its issuer. The BCL cannot hand a list to `X509Chain`, and
`CertificateRevocationListBuilder.Load` gives only the revoked serial numbers, not the issuer,
dates or signature.

Measured on 2026-09-29 with `Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile`, extended in
BL-609 with `-TlsEmptyCrlFile` and `-TlsRevokingCrlFile` (lists signed by the throwaway root,
which may now sign CRLs), `-sS --cacert root.pem --crlfile <file>`:

| Case | curl 8.21.0 Schannel (Windows) | curl 8.18.0 OpenSSL 3.5.5 (Ubuntu, WSL) |
| --- | --- | --- |
| no `--crlfile` | exit 60 `schannel: the revocation status is unknown` (ADR-0321) | exit 0 |
| a missing file | exit 2, the parser's refusal | exit 2, the parser's refusal |
| garbage, an empty file, a DER list, a directory | as without `--crlfile` | exit 82 `error loading CRL file: <path>` |
| an empty list from the root | as without `--crlfile` | exit 0 |
| a list revoking the server certificate | as without `--crlfile` | exit 60 `SSL certificate OpenSSL verify result: certificate revoked (23)` |
| a list from another CA | (not run) | exit 60 `... unable to get certificate CRL (3)` |
| a list in the root's name signed by another key | (not run) | exit 60 `... CRL signature failure (8)` |
| `--ssl-no-revoke` with the revoking list, or garbage | exit 0 | (not applicable) |
| `-k` with the revoking list, or garbage | exit 0 | exit 0 |

So the Schannel build ignores `--crlfile` entirely, and the OpenSSL build neither loads nor
checks it under `-k`.

## Decision

- `TlsClientOptions.CertificateRevocationListFile` carries the value; `Curl.Console` maps the
  target's `--crlfile` onto it (`--proxy-crlfile` is BL-611's).
- **The Schannel build ignores it**, as curl 8.21.0's does. On Windows, where the providers
  behave as the Schannel build, `--crlfile` changes nothing.
- **The OpenSSL build**, unless `-k`, loads the file in `ServerCertificateVerification.ReadTrustAnchors`,
  after the `--cacert` file, with `CertificateRevocationListFile.Load`: every PEM `X509 CRL`
  block, decoded by `CertificateRevocationList` with `System.Formats.Asn1`; a file that cannot be
  read, holds no such block, or holds one that does not decode is exit 82 with
  `TlsFailureMessages.OpenSslRevocationListFileUnusable`. The failure travels as
  `CertificateRevocationListFileException`, a `CryptographicException`, so the three places that
  read trust anchors (both providers and `QuicDialer`) keep one catch and ask
  `ServerCertificateVerification.TrustAnchorsUnusable` for exit 77 or 82.
- **The check runs in `ServerCertificateVerification.Judge`**, after `VerifyPeer` accepts the chain
  and before `--pinnedpubkey`, so both providers and the hand-built QUIC handshake share it. For
  each certificate of the built chain, the server's first, with the next certificate as its
  issuer (the root its own), it follows OpenSSL's `check_crl` and `cert_crl`: a list whose issuer
  name is byte for byte the certificate's issuer name (the first valid now, else the first), else
  3; the issuer's key usage, if present, must allow CRL signing, else 35; the list's
  `thisUpdate` not in the future (11) and `nextUpdate`, if any, not past (12); its RSA PKCS #1 or
  ECDSA signature over SHA-1, SHA-256, SHA-384 or SHA-512 verified with the issuer's key, else 8;
  the certificate's serial number not listed, else 23. The first refusal is exit 60 with
  `SSL certificate OpenSSL verify result: <OpenSSL's text> (<code>)`.
- Texts 3, 8 and 23 are measured; 11, 12 and 35 are OpenSSL's `X509_verify_cert_error_string`
  table in the same measured format.

## Consequences

- On Linux and macOS a revoked server certificate fails as curl's OpenSSL build fails it, and a
  bad `--crlfile` is exit 82; on Windows `--crlfile` is ignored, as curl.exe ignores it.
- Differences left: a chain that is also untrusted or out of date reports that error rather than
  the list's (OpenSSL keeps whichever it met last); list extensions (critical ones, delta lists,
  issuing distribution points) are read past; names are compared as encoded, not canonically.
- Tests generate the CA, certificates and lists with `CertificateRevocationListBuilder`, and the
  shapes it never writes with `Fakes/TestRevocationList`.

## Alternatives considered

- **Let the platform check revocation.** `X509Chain` takes no list file; on Linux .NET would
  fetch lists from the certificate's distribution points, which the curl build never does.
- **Read the lists with `CertificateRevocationListBuilder.Load`.** It returns only the entries,
  so the issuer, dates and signature, which decide 3, 8, 11 and 12, would go unchecked.
- **Check `--crlfile` in the Schannel build too.** Refuted by measurement: curl.exe ignores it.
