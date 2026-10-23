---
id: BL-3285
title: Measure and match Schannel curl on a P12 client certificate (tls-mtls-p12)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3285 — Measure and match Schannel curl on a P12 client certificate (tls-mtls-p12)

## Goal

`--cert client.p12:pw --cert-type P12` against a server requiring a client certificate behaves as Schannel curl 8.22.0 does, or an ADR records why Curl differs.

## Context

Found by BL-3235's CLI TLS differential run (case `tls-mtls-p12`, also in the tester's `report_tls.txt`): Curl exits 0 with `ok\n`; real curl exits 35 "schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The credentials supplied to the package were not recognized". Measure whether real curl fails for every P12 or only for this file's key type or encryption (the tester's `pki/client.p12`, made by OpenSSL 3 with AES-256 PBES2), and decide, recording the decision in an ADR.

## Acceptance criteria

- [ ] What real curl does with the P12 client certificate is measured and recorded under Notes, and Curl matches it or an ADR says why not.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
