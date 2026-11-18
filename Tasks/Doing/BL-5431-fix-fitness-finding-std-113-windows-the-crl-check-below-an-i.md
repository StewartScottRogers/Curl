---
id: BL-5431
title: Fix fitness finding STD-113: Windows: the CRL check below an intermediate-only --cacert / --proxy-cacert anchor ignores the CRL's scope and critical extensions, so a revoked certificate is accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateRevocationList.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5431 — Fix fitness finding STD-113: Windows: the CRL check below an intermediate-only --cacert / --proxy-cacert anchor ignores the CRL's scope and critical extensions, so a revoked certificate is accepted

## Goal

STD-113 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows: the CRL check below an intermediate-only --cacert / --proxy-cacert anchor ignores the CRL's scope and critical extensions, so a revoked certificate is accepted. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** High (security weakening: the CRL is fetched over plain http, so an on-path attacker can serve another list the same CA signed — its CA-only list, a partition or a delta list — that does not cover the certificate, and a revoked server or proxy certificate is accepted in the default configuration)
- **Confidence:** Confirmed (each variant at least 2 runs by the reviewer; 5 variants re-reproduced twice by the orchestrator)
- **Protocol/area:** HTTPS and HTTPS-proxy certificate revocation, Windows build (the ADR-1294 code path)
- **Repro:**
  1. PKI: `root.pem` self-signed CA. `intA.pem` signed by root: CA:TRUE, keyUsage keyCertSign+cRLSign. Leaves signed by intA: SAN `DNS:localhost`, EKU serverAuth, each with a CRL distribution point `http://127.0.0.1:40080/b/<variant>.crl`. Leaf `rev_idp_other` is also listed as revoked in intA's normal CRL.
  2. For each variant, serve over http at that URL a CRL signed by intA's key, issuer = intA's subject, current thisUpdate/nextUpdate, an AKI and a CRLNumber, and **no revoked entries**:
     - `idp_other`: critical IssuingDistributionPoint whose fullName is `URI:http://127.0.0.1:40080/partition/other.crl` (not the certificate's DP)
     - `caonly`: critical IDP with `onlyContainsCACerts=TRUE`
     - `somereasons`: critical IDP with `onlySomeReasons=keyCompromise`
     - `delta`: critical `deltaCRLIndicator`
     - `unkcrit`: an unknown critical CRL extension `1.3.6.1.4.1.99999.1`, value `05 00`
  3. Serve each leaf + intA over TLS on 127.0.0.1 (Python `ssl.SSLContext(PROTOCOL_TLS_SERVER).load_cert_chain`, answering `HTTP/1.1 200 OK` with body `hello <name>`).
  4. `curl -sS -m 20 https://localhost:<port>/ --cacert intA.pem`
  5. Proxy: an HTTPS forward proxy presenting `rev_idp_other` + intA, then `curl -sS -x https://localhost:<pport> --proxy-cacert intA.pem http://127.0.0.1:40999/x`
- **Expected (REF):** every variant and the proxy case: `curl: (60) schannel: the revocation status is unknown`, exit 60. Baseline: a CRL with only a non-critical unknown extension, or IDP `onlyContainsUserCerts=TRUE`, gives exit 0 in both.
- **Actual (PORT):** every variant prints `hello <name>`, exit 0 (the revoked `rev_idp_other` leaf included). The proxy case prints the proxied body, exit 0. Same through the `--ciphers` hand-built TLS path.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateRevocationList.cs` `Decode` ("Extensions … are read past and not acted on"); `ServerCertificateVerification.AnchorRevocation.cs` `IsCurrentListOf` / `UsableRevocationList`. Treat a list as unusable ("unknown") when it has any unrecognised critical list or entry extension, a deltaCRLIndicator, or an IDP that does not match the certificate (DP name, onlyContainsCACerts / onlyContainsUserCerts against basicConstraints, onlySomeReasons, indirectCRL).
- **Acceptance check:** the five variant commands and the proxy command exit 60 with `schannel: the revocation status is unknown`. A CRL with only non-critical unknown extensions, and one with IDP `onlyContainsUserCerts=TRUE`, still exit 0.

## Acceptance criteria

- [ ] the five variant commands and the proxy command exit 60 with `schannel: the revocation status is unknown`. A CRL with only non-critical unknown extensions, and one with IDP `onlyContainsUserCerts=TRUE`, still exit 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-113 (High).
- 2026-11-18: Backlog -> Doing. Criteria at claim: e3c4b96a 69116afe 3d836d5e.
