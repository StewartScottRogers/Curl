---
id: BL-5451
title: Fix fitness finding STD-101: Linux: wrong verify-result text when the name-constrained intermediate is itself the trust anchor (STILL OPEN, narrowed: root-anchored cases now report 47/48 correctly)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5451 — Fix fitness finding STD-101: Linux: wrong verify-result text when the name-constrained intermediate is itself the trust anchor (STILL OPEN, narrowed: root-anchored cases now report 47/48 correctly)

## Goal

STD-101 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux: wrong verify-result text when the name-constrained intermediate is itself the trust anchor (STILL OPEN, narrowed: root-anchored cases now report 47/48 correctly). Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (fails closed; wrong message and `%{ssl_verify_result}`)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** HTTPS / HTTPS-proxy `--cacert`, `--proxy-cacert`, `--capath`, `SSL_CERT_DIR`, Linux build
- **Repro (Linux/WSL):** openssl PKI: root; `intnc` (CA, signed by root, `nameConstraints=critical,permitted;DNS:example.com`) with `leafnc` (SAN `DNS:localhost,IP:127.0.0.1`, serverAuth) served with intnc on 127.0.0.1:41106 and as an HTTPS proxy answering 200 on 41123; `intexcl` (`excluded;DNS:localhost`) with `leafexcl` on 41107; `intncip` (`permitted;IP:10.0.0.0/255.0.0.0`) with `leafip` (SAN `IP:127.0.0.1`) on 41108. `capath_nc` holds only intnc, `capath_excl` only intexcl (both `openssl rehash`).
  - `curl -sS -w '%{ssl_verify_result}\n' --cacert intnc.pem https://localhost:41106/`
  - `curl -sS --cacert intexcl.pem https://localhost:41107/`
  - `curl -sS --cacert intncip.pem https://127.0.0.1:41108/`
  - `curl -sS --capath capath_nc https://localhost:41106/`; `SSL_CERT_DIR=capath_nc curl -sS https://localhost:41106/`; `curl -sS --capath capath_excl https://localhost:41107/`
  - `curl -sS --proxy-cacert intnc.pem -x https://localhost:41123 http://example.invalid/`
- **Expected (REF2):** `curl: (60) SSL certificate OpenSSL verify result: permitted subtree violation (47)` (41106, 41108, proxy) and `excluded subtree violation (48)` (41107); `%{ssl_verify_result}` 47 / 48.
- **Actual (PORT):** exit 60 but `unable to get local issuer certificate (20)` and `%{ssl_verify_result}` 20 in every case.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs` `CaCertificateFileAnchorsChain`: when `CertificateNameConstraints.FindViolation(chain)` is non-zero the chain is refused but the reason stays .NET's partial-chain status. Feed 47/48 into `TlsFailureMessages.OpenSslVerifyError` / `OpenSslVerifyResult.cs`; same on the `--capath` path once STD-121 is fixed.
- **Acceptance check:** the 41106, 41108 and proxy commands print `(47)`, the 41107 commands print `(48)`, all exit 60, with `%{ssl_verify_result}` 47 or 48.

## Acceptance criteria

- [ ] the 41106, 41108 and proxy commands print `(47)`, the 41107 commands print `(48)`, all exit 60, with `%{ssl_verify_result}` 47 or 48.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-101 (Low).