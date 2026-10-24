---
id: BL-3290
title: Measure and match Schannel curl on a P12 client certificate (tls-mtls-p12)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3290 — Measure and match Schannel curl on a P12 client certificate (tls-mtls-p12)

## Goal

`--cert client.p12:pw --cert-type P12` against a server requiring a client certificate behaves as Schannel curl 8.22.0 does, or an ADR records why Curl differs.

## Context

Found by BL-3235's CLI TLS differential run (case `tls-mtls-p12`, also in the tester's `report_tls.txt`): Curl exits 0 with `ok\n`; real curl exits 35 "schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The credentials supplied to the package were not recognized". Measure whether real curl fails for every P12 or only for this file's key type or encryption (the tester's `pki/client.p12`, made by OpenSSL 3 with AES-256 PBES2), and decide, recording the decision in an ADR.

## Acceptance criteria

- [x] What real curl does with the P12 client certificate is measured and recorded under Notes, and Curl matches it or an ADR says why not.
- [x] The build is clean and the fast tests pass.

## Notes

- Measured 2026-10-23 (Windows 11 26H2 build 26300, loopback `SslStream` server requiring a client certificate, throwaway C# file-based apps outside the repo): curl 8.22.0 (ucrt64 Schannel) exits 35 `schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d)` for every P12. That covers the tester's ECDSA P-256 AES-256 PBES2 file, CA-issued RSA-2048 and ECDSA files with 3DES or AES-256 and with `pw` or an empty password, and a self-signed RSA P12 with an empty password (ADR-0009's case, which exited 0 on 2026-09-26). The result is the same with `-k` and without `--cert-type`. `C:\Windows\System32\curl.exe` 8.21.0 fails the same way. It is not the key type or the encryption: Schannel on this OS build refuses the credential. A bad password still gives 58 `password is bad`. A closed port still gives 7, so the refusal comes after the connect.
- Decision (ADR-0794, decided by Claude under Stewart's delegation): Curl keeps presenting the P12 and does not copy exit 35. The failure comes from the OS build, not from curl, and copying it would leave no file-based client certificate that works on Windows. No code change in Curl.Tls.UnitLibrary, so Measure-CodeQuality was not needed.
- Follow-up: BL-3313 tags `tls-mtls-p12` in the differential runner (Curl.Conformance.Differential.UnitLibrary, outside this task's touches).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: df7eab68 c46ca32a.
- 2026-10-23: Doing -> Done. Real Schannel curl's exit 35 for every P12 is measured; ADR-0794 keeps Curl presenting P12 client certificates
