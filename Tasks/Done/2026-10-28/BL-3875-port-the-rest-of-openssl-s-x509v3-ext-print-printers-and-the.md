---
id: BL-3875
title: Port the rest of OpenSSL's X509V3_EXT_print printers and the DSA and DH key records for %{certs}
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3620]
touches: [Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs, Curl.Output.UnitTests/OpenSslPeerCertificateTextTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3875 — Port the rest of OpenSSL's X509V3_EXT_print printers and the DSA and DH key records for %{certs}

## Goal

Off Windows, `-w %{certs}` prints every extension a public web certificate carries, and a DSA or DH key, as curl's OpenSSL build does.

## Context

BL-3620 (ADR-0926) added `OpenSslPeerCertificateText`, the OpenSSL build's `ossl_certchain` layout, with printers only for the subject and authority key identifiers, basic constraints, key usage, extended key usage and the alternative names. Every other extension prints its raw value with dots, and its name dotted; curl 8.18.0 with OpenSSL 3.5.5 names and prints, among others, `X509v3 CRL Distribution Points`, `Authority Information Access`, `X509v3 Certificate Policies`, `CT Precertificate SCTs`, `X509v3 Name Constraints`, `X509v3 Policy Constraints`, `X509v3 Inhibit Any Policy`, `Netscape Comment` and `Netscape Cert Type` (several multi-line). `ossl_certchain` also prints `dsa(p)`, `dsa(q)`, `dsa(g)`, `dsa(pub_key)` for a DSA key and `dh(p)`, `dh(q)`, `dh(g)`, `dh(pub_key)` for a PKCS #3 DH key, which the port leaves out. Measure in WSL as BL-3620's Notes describe (`openssl s_server -WWW` and `curl -s -k -o /dev/null -w '%{certs}'`), with a certificate carrying each extension (a Let's Encrypt certificate carries AIA, policies and SCTs).

## Acceptance criteria

- [x] Each extension listed in Context, measured through real curl's OpenSSL build, prints the same record through `PeerCertificateText.FormatFor(certificate, TlsBackend.OpenSsl)`; the commands and both results are in Notes.
- [x] A DSA key's and a DH key's records match the OpenSSL build's (measured, or from `lib/vtls/openssl.c` where OpenSSL 3.5 cannot serve the key).
- [x] Unit tests in `Curl.Output.UnitTests/OpenSslPeerCertificateTextTests.cs` pin each record.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports no failing member.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured in WSL (curl 8.18.0, OpenSSL 3.5.5): a self-signed certificate per extension from `openssl req -x509 -config` with the extension in `[ext]`, served by `openssl s_server -www -cert c.pem -key k.pem -accept 4433`, read with `curl -s -k -o /dev/null -w '%{certs}' https://localhost:4433/`. Results are the expected strings pinned in `OpenSslPeerCertificateTextTests` (CRL Distribution Points, AIA, SIA, Certificate Policies, Name Constraints, Policy Constraints, Inhibit Any Policy, Netscape Comment and Cert Type, CT Precertificate SCTs with a hand-built valid SCT, Freshest CRL with reasons and CRL issuer). Each test builds the same extension's DER and compares `FormatFor(..., TlsBackend.OpenSsl)` to the measured record.
- Not servable by OpenSSL 3.5's config, so from OpenSSL source (`v3_crld.c`, `v3_cpols.c`, `v3_sct`/`ct_prn.c`): relative-name distribution points, `<none>` reasons, unknown policy qualifiers, anyPolicy's name, an SCT of unknown version, SCT signature names other than SHA-256 ECDSA/RSA.
- DSA and DH: from `lib/vtls/openssl.c` `get_pkey_dsa` and `get_pkey_dh` (`pubkey_show`): `dsa(p)`, `dsa(q)`, `dsa(g)`, `dsa(pub_key)`; `dh(p)`, `dh(q)`, `dh(g)`, `dh(pub_key)` where `pubkey_show` pushes the record even for a NULL number, so a PKCS #3 key's `dh(q)` is empty. No size line, unlike RSA. A server cannot serve a DSA or DH certificate key over TLS 1.3 so these were not measured.
- Decision: a printer's trailing line feed is trimmed (the measured records show none before the next record). Directory names in CRL DP and name constraints use `X509_NAME_print_ex` (`DirName:CN = Issuer`, measured); in AIA and AKID they stay `X509_NAME_oneline`.
- `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary`: 0 failing members. Fast tests for Curl.Output.UnitTests pass (865 passed, 4 skipped). The tests are platform-neutral.
## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 68d6306a 55be06fb 20a1f7bc c25bccc9 779790c0.
- 2026-10-28: Doing -> Done. OpenSSL-build %{certs} prints CRL DP, AIA/SIA, policies, constraints, SCTs, Netscape extensions and DSA/DH key records
