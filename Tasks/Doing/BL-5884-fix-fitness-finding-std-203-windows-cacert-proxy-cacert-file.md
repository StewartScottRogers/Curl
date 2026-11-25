---
id: BL-5884
title: Fix fitness finding STD-203: Windows --cacert / --proxy-cacert file with a malformed CERTIFICATE block: the port trusts the parsable rest, or silently drops the whole file; curl refuses with exit 77
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5884 — Fix fitness finding STD-203: Windows --cacert / --proxy-cacert file with a malformed CERTIFICATE block: the port trusts the parsable rest, or silently drops the whole file; curl refuses with exit 77

## Goal

STD-203 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Windows --cacert / --proxy-cacert file with a malformed CERTIFICATE block: the port trusts the parsable rest, or silently drops the whole file; curl refuses with exit 77. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

Shared setup: These findings share one harness, kept in the reviewer's scratchpad as `work/d1/` (`gen.py`, `gen2.py`, `mkcrl.py`, `build.sh`, `servers.py`, `cmp.sh`). A self-signed `root.pem` signs intermediates `intA.pem`, `intCRL.pem` and `intOCSP.pem` (each with a CDP to a clean root CRL). An HTTP server on `127.0.0.1:31080` serves CRLs and answers OCSP requests; every leaf has SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and is served by its own TLS server answering `hello <leaf>`. **Windows caches fetched CRL/OCSP responses per URL for all processes**, so clear the cache before each run (`certutil -urlcache <url> delete` for every `127.0.0.1:31080` entry), or REF's cached answers leak into the port's run.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs, direct and proxy) · **Area:** HTTPS CA file parsing, Windows build.
- **Files** (from the shared PKI's `root.pem`, which signs the server's leaf):
  - `rootbad64.pem`: `root.pem` followed by a `-----BEGIN CERTIFICATE-----` block whose body is `!!!!notbase64!!!!`
  - `roottrunc.pem`: `root.pem` followed by a lone `-----BEGIN CERTIFICATE-----` line
  - `rootgarb.pem`: `root.pem` followed by a CERTIFICATE block whose base64 body is `AAAA`
- **Repro:** `curl -sS --ssl-no-revoke --cacert <file> https://localhost:<port>/` (and the `-x https://… --proxy-cacert <file>` form).
- **Expected (REF):** `rootbad64`: `curl: (77) schannel: failed to extract certificate from CA file 'rootbad64.pem': Cannot find the requested object.`; `roottrunc`: `curl: (77) schannel: CA file 'roottrunc.pem' is not correctly formatted`; `rootgarb`: exit 77 with the same text as `rootbad64`.
- **Actual (.NET):** `rootbad64` and `roottrunc`: exit 0, the root is trusted. `rootgarb` (and the same block placed before the root): exit 60 `the certificate chain is incomplete` — the root before the bad block is discarded, despite the code comment "what was imported before it is kept".
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs`, `ReadCaCertificates` (Schannel branch) and `ImportParsableCertificates`.
- **Acceptance check:** all three files give exit 77 with REF's message text.

## Acceptance criteria

- [ ] all three files give exit 77 with REF's message text.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 9e20ff72 69116afe 3d836d5e.
