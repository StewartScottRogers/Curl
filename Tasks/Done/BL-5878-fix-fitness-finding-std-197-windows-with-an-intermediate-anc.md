---
id: BL-5878
title: Fix fitness finding STD-197: Windows: with an intermediate anchor and --ssl-revoke-best-effort, a certificate revoked by an RSA-PSS-signed CRL, or by a CRL with no nextUpdate, is accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateRevocationList.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitLibrary/RsaPssSignatureParameters.cs, Curl.Networking.UnitTests/RsaPssSignatureParametersTests.cs, Curl.Networking.UnitTests/IntermediateOnlyProxyCacertTests.cs, Curl.Networking.UnitTests/Fakes/TestRevocationList.cs, Documentation/Planning/Decisions/ADR-1294-intermediate-only-cacert-revocation-is-read-from-the-crl.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
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

- [x] both repro commands exit 60 with "…has been revoked"; `curl -sS https://localhost:<port of c_pss_good>/ --cacert intCRL.pem` and the same for `c_nonext_good` exit 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: `CertificateRevocationList.IsSignedBy` verifies RSASSA-PSS (new `RsaPssSignatureParameters.cs`), and a base or delta list naming no `nextUpdate` is current (`IsUnexpiredAt`). Decision: ADR-1357, which replaces ADR-1294's "no nextUpdate fails closed" (a status line added there).
- Measured 2026-11-24 with a temporary C# file-based app (certificates, CRLs on loopback, one TLS server per leaf), Curl's debug build beside Git's curl 8.22.0 Schannel, strict and `--ssl-revoke-best-effort`: all 16 answers match (both revoked leaves exit 60 "has been revoked", both good leaves exit 0). The original did not need a separate reproduction run: the code at claim time rejected both list shapes by inspection.
- `touches` widened (no other task in Doing names these): `RsaPssSignatureParameters.cs`, and in `Curl.Networking.UnitTests` `RsaPssSignatureParametersTests.cs`, `IntermediateOnlyProxyCacertTests.cs` (two tests pinned the old fail-closed answer) and `Fakes/TestRevocationList.cs`; and ADR-1294.
- Salt lengths other than the hash length, which the base class library cannot verify, are filed as BL-5922.
- Verification: `dotnet build` clean, fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: fbe06d3e 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. STD-197 fixed: RSASSA-PSS CRLs verify and a CRL with no nextUpdate is current (ADR-1357); matches curl 8.22.0 Schannel end to end
