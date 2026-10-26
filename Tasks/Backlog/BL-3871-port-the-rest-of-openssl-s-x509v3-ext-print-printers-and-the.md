---
id: BL-3871
title: Port the rest of OpenSSL's X509V3_EXT_print printers and the DSA and DH key records for %{certs}
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3620]
touches: [Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs, Curl.Output.UnitTests/OpenSslPeerCertificateTextTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3871 — Port the rest of OpenSSL's X509V3_EXT_print printers and the DSA and DH key records for %{certs}

## Goal

Off Windows, `-w %{certs}` prints every extension a public web certificate carries, and a DSA or DH key, as curl's OpenSSL build does.

## Context

BL-3620 (ADR-0825) added `OpenSslPeerCertificateText`, the OpenSSL build's `ossl_certchain` layout, with printers only for the subject and authority key identifiers, basic constraints, key usage, extended key usage and the alternative names. Every other extension prints its raw value with dots, and its name dotted; curl 8.18.0 with OpenSSL 3.5.5 names and prints, among others, `X509v3 CRL Distribution Points`, `Authority Information Access`, `X509v3 Certificate Policies`, `CT Precertificate SCTs`, `X509v3 Name Constraints`, `X509v3 Policy Constraints`, `X509v3 Inhibit Any Policy`, `Netscape Comment` and `Netscape Cert Type` (several multi-line). `ossl_certchain` also prints `dsa(p)`, `dsa(q)`, `dsa(g)`, `dsa(pub_key)` for a DSA key and `dh(p)`, `dh(q)`, `dh(g)`, `dh(pub_key)` for a PKCS #3 DH key, which the port leaves out. Measure in WSL as BL-3620's Notes describe (`openssl s_server -WWW` and `curl -s -k -o /dev/null -w '%{certs}'`), with a certificate carrying each extension (a Let's Encrypt certificate carries AIA, policies and SCTs).

## Acceptance criteria

- [ ] Each extension listed in Context, measured through real curl's OpenSSL build, prints the same record through `PeerCertificateText.FormatFor(certificate, TlsBackend.OpenSsl)`; the commands and both results are in Notes.
- [ ] A DSA key's and a DH key's records match the OpenSSL build's (measured, or from `lib/vtls/openssl.c` where OpenSSL 3.5 cannot serve the key).
- [ ] Unit tests in `Curl.Output.UnitTests/OpenSslPeerCertificateTextTests.cs` pin each record.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports no failing member.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
