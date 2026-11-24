---
id: BL-5878
title: Fix fitness finding STD-197: Windows: with an intermediate anchor and --ssl-revoke-best-effort, a certificate revoked by an RSA-PSS-signed CRL, or by a CRL with no nextUpdate, is accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateRevocationList.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5878 — Fix fitness finding STD-197: Windows: with an intermediate anchor and --ssl-revoke-best-effort, a certificate revoked by an RSA-PSS-signed CRL, or by a CRL with no nextUpdate, is accepted

## Goal

STD-197 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows: with an intermediate anchor and --ssl-revoke-best-effort, a certificate revoked by an RSA-PSS-signed CRL, or by a CRL with no nextUpdate, is accepted. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** High · **Confidence:** Confirmed (2 runs; re-verified by the orchestrator) · **Area:** HTTPS / HTTPS-proxy CRL revocation, Windows build, non-self-signed anchor.
- **Certificates:** `intCRL.pem` is an intermediate under `root.pem`.
  - `c_pss_rev` and `c_pss_good`: CDP `http://127.0.0.1:31080/c/pss.crl`. That CRL is issued by intCRL, signed **RSASSA-PSS** (SHA-256, MGF1-SHA256, salt 32), current, and lists `c_pss_rev`.
  - `c_nonext_rev` and `c_nonext_good`: CDP `http://127.0.0.1:31080/c/nonext.crl`. That CRL is issued by intCRL, SHA-256 RSA PKCS#1, has **no nextUpdate field** (optional in RFC 5280), and lists `c_nonext_rev`.
- **Repro** (URL cache cleared first):
  - `curl -sS https://localhost:<port of c_pss_rev>/ --cacert intCRL.pem --ssl-revoke-best-effort`
  - `curl -sS https://localhost:<port of c_nonext_rev>/ --cacert intCRL.pem --ssl-revoke-best-effort`
- **Expected (REF):** exit 60, `schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.
- **Actual (.NET):** exit 0, `hello c_pss_rev` / `hello c_nonext_rev`.
- **Without `--ssl-revoke-best-effort`:** the revoked leaves exit 60 with "the revocation status is unknown" (REF: "…has been revoked"), and the good leaves `c_pss_good` / `c_nonext_good` exit 60 "the revocation status is unknown" where REF exits 0. Through `--proxy-cacert` the port also says "unknown" where REF says "revoked".
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateRevocationList.cs`: `IsSignedBy` uses only `RSASignaturePadding.Pkcs1` and its signature-algorithm OID map lacks `1.2.840.113549.1.1.10` (RSASSA-PSS, with parameters). `ServerCertificateVerification.AnchorRevocation.cs` `IsCurrentListOf` requires `NextUpdate >= now`, so a CRL without nextUpdate is never current. Both make the list "Unresolved", which best-effort then tolerates. Verify PSS signatures (parse the PSS parameters) and treat an absent nextUpdate as current, as Windows does.
- **Acceptance check:** both repro commands exit 60 with "…has been revoked"; `curl -sS https://localhost:<port of c_pss_good>/ --cacert intCRL.pem` and the same for `c_nonext_good` exit 0.

## Acceptance criteria

- [ ] both repro commands exit 60 with "…has been revoked"; `curl -sS https://localhost:<port of c_pss_good>/ --cacert intCRL.pem` and the same for `c_nonext_good` exit 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
