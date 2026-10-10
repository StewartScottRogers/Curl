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
completed:
---
# BL-1923 — Generate upstream's tests/certs certificates from the vendored prm files for the upstream case runner

## Goal

The files upstream's cases read through `%CERTDIR/certs/` (test-ca.crt, test-ca.cacert, test-localhost.pem/.crt/.der/.pub.pem/.pub.der/.crl, test-localhost.nn.*, test-localhost0h.pem, test-localhost-san-first/last.pem, test-client-cert.crt/.key, test-client-eku-only.crt/.key) exist under the certificate directory the conformance tests pass to UpstreamCaseRunner, generated with the BCL from the vendored .prm files.

## Context

Split from BL-1922. The curl 8.21.0 tarball's tests/certs holds no certificate files: upstream generates them at build time with genserv.pl (OpenSSL) from the .prm parameter files. BL-1922 vendored the sources (genserv.pl, test-ca.cnf, srp-verifier-*, test-*.prm) in Curl.Conformance.UnitTests\UpstreamTestData\certs and made %CERTDIR resolve to UpstreamTestData, so `%CERTDIR/certs/<name>` names a file that does not exist yet. Write a generator (System.Security.Cryptography.X509Certificates.CertificateRequest) that reads each .prm as genserv.pl does and writes the same file set into a folder the test run creates (e.g. under the test output), signed by a generated test CA; pass that folder's parent as the certificate directory. Every %CERTDIR case also needs an https/https-mtls/http3 server (TlsServerStream from BL-1921, stand-ins BL-1912 to BL-1914), so none runs until those exist; the server's certificate must be the one generated here.

## Acceptance criteria

- [ ] Each certificate file named by a vendored %CERTDIR case is generated, with the subject, SANs and extensions its .prm names (a unit test per kind pins them).
- [ ] UpstreamConformanceTests passes the generated folder's parent as the certificate directory.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity at most 10; tests are platform-neutral.

## Notes

## Log

- 2026-10-09: Created.
