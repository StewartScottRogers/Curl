---
id: BL-5877
title: Fix fitness finding STD-196: Windows: with an intermediate --cacert / --proxy-cacert anchor, OCSP is never consulted, so an OCSP-revoked certificate is accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorOcsp.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs, Curl.Networking.UnitLibrary/RevocationListDownload.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/ServerCertificateVerificationTests.AnchorOcsp.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5877 — Fix fitness finding STD-196: Windows: with an intermediate --cacert / --proxy-cacert anchor, OCSP is never consulted, so an OCSP-revoked certificate is accepted

## Goal

STD-196 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows: with an intermediate --cacert / --proxy-cacert anchor, OCSP is never consulted, so an OCSP-revoked certificate is accepted. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (2 runs each, direct and proxy; re-verified by the orchestrator) · **Area:** HTTPS / HTTPS-proxy revocation, Windows build, non-self-signed anchor.
- **Certificates:** `intOCSP.pem` is an intermediate under `root.pem`. Leaf `oc_crlclean_ocsprev`: CDP `http://127.0.0.1:31080/c/oc.crl` serving a valid, current CRL from intOCSP that does **not** list it; AIA OCSP `http://127.0.0.1:31080/ocsp/oc_crlclean_ocsprev`, whose responder answers "revoked, keyCompromise", signed by intOCSP. Leaf `o_revoked`: no CDP, AIA OCSP answers "revoked". Leaf `o_good`: no CDP, AIA OCSP answers "good".
- **Repro** (URL cache cleared first):
  - `curl -sS https://localhost:<port of oc_crlclean_ocsprev>/ --cacert intOCSP.pem`
  - `curl -sS -x https://localhost:<port of oc_crlclean_ocsprev> --proxy-cacert intOCSP.pem http://target.invalid/`
  - `curl -sS https://localhost:<port of o_revoked>/ --cacert intOCSP.pem --ssl-revoke-best-effort`
- **Expected (REF 8.22.0 Schannel):** all three exit 60 with `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.
- **Actual (.NET):** all three exit 0 and print `hello oc_crlclean_ocsprev` / `hello o_revoked`.
- **Same root cause, valid certificates refused:** `o_good`, a Must-Staple OCSP-only good leaf, and a leaf whose CRL URL returns 404 but whose OCSP says good: REF exits 0, the port exits 60 `schannel: the revocation status is unknown`. A leaf listed on its CRL but "good" by OCSP: REF exits 0 (Windows prefers OCSP), the port exits 60 "has been revoked".
- **With `--cacert root.pem`** (self-signed anchor, Windows chain engine) the port matches REF in every one of these cases.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs`: `RevocationOf` and `WorkOutBelowAnchorRevocation` read only CRL distribution points and never the AIA OCSP responder (`id-ad-ocsp`). Add an OCSP check (response signed by the issuer or a delegated responder with `id-kp-OCSPSigning`, `thisUpdate`/`nextUpdate` current), with OCSP preferred over CRL as Windows does.
- **Acceptance check:** `curl -sS https://localhost:<port of oc_crlclean_ocsprev>/ --cacert intOCSP.pem` exits 60 with "…has been revoked" (also via `--proxy-cacert`); `curl -sS https://localhost:<port of o_good>/ --cacert intOCSP.pem` exits 0.

## Acceptance criteria

- [x] `curl -sS https://localhost:<port of oc_crlclean_ocsprev>/ --cacert intOCSP.pem` exits 60 with "…has been revoked" (also via `--proxy-cacert`); `curl -sS https://localhost:<port of o_good>/ --cacert intOCSP.pem` exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- OCSP check added in the new `ServerCertificateVerification.AnchorOcsp.cs`, reusing `Curl.Tls`'s `OcspStapleVerifier`; OCSP is asked first and the CRL only when no responder says good or revoked (ADR-1347).
- `touches` widened to that file, its test file, the fetch adapter's and chain errors' comments and the library's CLAUDE.md; no other task in Doing named any of them.
- Verified 2026-11-24 with a throwaway C# mock (RSA root and intermediate, TLS servers, OCSP/CRL responder on 127.0.0.1): Curl and curl.exe (Schannel, Git for Windows) both exit 60 "has been revoked" for oc_crlclean_ocsprev direct and via --proxy-cacert and for o_revoked with --ssl-revoke-best-effort, and exit 0 for o_good direct and via proxy.
- Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line and branch, 0 failing members. Build clean, fast tests pass.


## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 2a4f5f42 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. OCSP is consulted below a non-self-signed --cacert/--proxy-cacert anchor and preferred over the CRL
