---
id: BL-5886
title: Fix fitness finding STD-205: Windows, intermediate anchor: a CRL distribution point that redirects to file:// aborts the handshake with exit 35
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/RevocationListDownload.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5886 — Fix fitness finding STD-205: Windows, intermediate anchor: a CRL distribution point that redirects to file:// aborts the handshake with exit 35

## Goal

STD-205 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows, intermediate anchor: a CRL distribution point that redirects to file:// aborts the handshake with exit 35. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (3 runs) · **Area:** HTTPS CRL fetching, Windows build.
- **Setup:** leaf `c_redirhttps_rev` under `intCRL.pem`, CDP `http://127.0.0.1:31080/rh/a.crl`, which answers `302 Location: file:///<dir>/c/rc.crl`.
- **Repro:** `curl -sS https://localhost:<port>/ --cacert intCRL.pem`, and the same with `--ssl-revoke-best-effort`.
- **Expected (REF):** exit 60 `schannel: the revocation status is unknown`; with `--ssl-revoke-best-effort`, exit 0.
- **Actual (.NET):** exit 35 `schannel: failed to receive handshake, SSL/TLS connection failed` in both cases. `--log-level warning` logs no "CRL … could not be fetched" line, so the exception escapes the fetch's catch. With `--cacert root.pem` the port matches REF.
- **Likely location:** `RevocationListDownload.Fetch` / `ServerCertificateVerification.AnchorRevocation.cs` `TryFetchRevocationList`: the catch filter misses the exception thrown for a redirect to a non-HTTP scheme.
- **Acceptance check:** the repro exits 60 "the revocation status is unknown", and exits 0 with `--ssl-revoke-best-effort`.

## Acceptance criteria

- [ ] the repro exits 60 "the revocation status is unknown", and exits 0 with `--ssl-revoke-best-effort`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
