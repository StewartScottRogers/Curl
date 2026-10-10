---
id: BL-1923
title: Generate upstream's tests/certs certificates from the vendored prm files for the upstream case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1923 — Generate upstream's tests/certs certificates from the vendored prm files for the upstream case runner

## Goal

The files upstream's cases read through `%CERTDIR/certs/` (test-ca.crt, test-ca.cacert, test-localhost.pem/.crt/.der/.pub.pem/.pub.der/.crl, test-localhost.nn.*, test-localhost0h.pem, test-localhost-san-first/last.pem, test-client-cert.crt/.key, test-client-eku-only.crt/.key) exist under the certificate directory the conformance tests pass to UpstreamCaseRunner, generated with the BCL from the vendored .prm files.

## Context

Split from BL-1922. The curl 8.21.0 tarball's tests/certs holds no certificate files: upstream generates them at build time with genserv.pl (OpenSSL) from the .prm parameter files. BL-1922 vendored the sources (genserv.pl, test-ca.cnf, srp-verifier-*, test-*.prm) in Curl.Conformance.UnitTests\UpstreamTestData\certs and made %CERTDIR resolve to UpstreamTestData, so `%CERTDIR/certs/<name>` names a file that does not exist yet. Write a generator (System.Security.Cryptography.X509Certificates.CertificateRequest) that reads each .prm as genserv.pl does and writes the same file set into a folder the test run creates (e.g. under the test output), signed by a generated test CA; pass that folder's parent as the certificate directory. Every %CERTDIR case also needs an https/https-mtls/http3 server (TlsServerStream from BL-1921, stand-ins BL-1912 to BL-1914), so none runs until those exist; the server's certificate must be the one generated here.

## Acceptance criteria

- [x] Each certificate file named by a vendored %CERTDIR case is generated, with the subject, SANs and extensions its .prm names (a unit test per kind pins them).
- [x] UpstreamConformanceTests passes the generated folder's parent as the certificate directory.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10; tests are platform-neutral.

## Notes

- Plan: `UpstreamCertificateParameters` reads a .prm as OpenSSL does (sections, key = value in order, # comments); `UpstreamTestCertificateGenerator` follows genserv.pl with `CertificateRequest`: P-256 keys (genserv.pl's prime256v1), SHA-256, CA 6000 days, leaves 300 days, each x509v3 key written in file order (basicConstraints, keyUsage, extendedKeyUsage, subjectAltName incl. raw DER:, SKI hash, AKI keyid, AIA caIssuers, CRL distribution points), and a CRL per leaf revoking it (CertificateRevocationListBuilder).
- Defaults taken: .cacert/.crt hold the PEM block only, without OpenSSL's `-text` dump, since curl reads only the block; the CRL carries the builder's AKI but not crl_ext's AIA; serials are 16 random bytes. Generation uses the injected TimeProvider.
- UpstreamConformanceTests generates once per run (Lazy) into `<test output>/UpstreamCertificates/certs` and passes `UpstreamCertificates` as %CERTDIR. No case changed verdict: every %CERTDIR case still skips for its https/https-mtls/http3 server (BL-1912 to BL-1914).
- Measured: Measure-CodeQuality -Library Curl.Conformance.UnitLibrary shows no failing member in the two new files (the library's 29 failing members are pre-existing). Subjects are pinned in DER order, not by X509Certificate2.Subject, whose text order differs by platform.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. The upstream case runner's %CERTDIR now holds certificates generated with the BCL from the vendored .prm files
